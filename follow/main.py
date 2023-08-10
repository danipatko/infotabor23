from websocket import create_connection
import tensorflow.lite as tflite
from cap import VideoCapture
from PIL import Image
import numpy as np
import argparse
import signal
import cv2

parser = argparse.ArgumentParser()
parser.add_argument('--ipcam', type=str, default="http://10.6.9.97:4747/video", help='MJPEG stream host address (droidcam server)')
parser.add_argument('--host', type=str, default="ws://roland:1111/ws", help='Roland websocket address')

CAMERA_WIDTH = 640
CAMERA_HEIGHT = 480
CENTER = CAMERA_WIDTH // 2
SPEED = 0.6
# number of frames to drop tracker
SANITYCHECK = 10

global ws

msgid = 1

def cleanup(*args):
    stop()
    ws.close()
    print("quit")
    exit(0)

def stop():
    global msgid
    ws.send(f'{msgid} s')
    # print(f'{msgid} s')
    msgid += 1

def move(left, right):
    global msgid
    ws.send(f'{msgid} m {left} {right}')
    # print(f'{msgid} m {left} {right}')
    msgid += 1

signal.signal(signal.SIGINT, cleanup)
signal.signal(signal.SIGTERM, cleanup) 

def sigmoid(x):
    return 1.0 / (1.0 + 1.0 / np.exp(x))

def load_model(model_path):
    interpreter = tflite.Interpreter(model_path=model_path)
    interpreter.allocate_tensors()
    return interpreter

def process_image(interpreter, image, input_index):
    input_data = np.expand_dims(image, axis=0)  # expand to 4-dim
    input_data = (np.float32(input_data) - 127.5) / 127.5  # float point

    # Process
    interpreter.set_tensor(input_index, input_data)
    interpreter.invoke()

    # Get outputs
    output_details = interpreter.get_output_details()

    output_data = np.squeeze(interpreter.get_tensor(output_details[0]['index']))
    offset_data = np.squeeze(interpreter.get_tensor(output_details[1]['index']))

    points = []
    total_row, total_col, total_points = output_data.shape

    for k in range(0, total_points):
        max_score = output_data[0][0][k]
        max_row = 0
        max_col = 0
        for row in range(total_row):
            for col in range(total_col):
                if (output_data[row][col][k] > max_score):
                    max_score = output_data[row][col][k]
                    max_row = row
                    max_col = col

        points.append((max_row, max_col))

    positions = []
    confidence = 0

    for idx, point in enumerate(points):
        pos_y, pos_x = point

        # y is row, x is column
        offset_x = offset_data[pos_y][pos_x][idx + 17]
        offset_y = offset_data[pos_y][pos_x][idx]

        positions.append((pos_x, pos_y, offset_x, offset_y))
        confidence += sigmoid(output_data[pos_y][pos_x][idx])

    confidence /= len(points)
    return positions, confidence

# avg of keypoints
def detect_position(positions, frame):
    sum_x = 0
    sum_y = 0
    offset = 5
    (sx, sy, ex, ey) = (CAMERA_WIDTH, CAMERA_HEIGHT, -CAMERA_WIDTH, -CAMERA_HEIGHT)

    for pos in positions:
        pos_x, pos_y, offset_x, offset_y = pos

        # Calculating the x and y coordinates
        x = int(pos_x / 8 * CAMERA_WIDTH + offset_x)
        y = int(pos_y / 8 * CAMERA_HEIGHT + offset_y)

        # avg
        sum_x += x
        sum_y += y

        # min/max => bounding box
        sx = min(sx, x)
        sy = min(sy, y)
        ex = max(ex, x)
        ey = max(ey, y)
    
    res = (sum_x // len(positions), sum_y // len(positions))
    bbox = (sx - offset, sy - offset, ex - sx + (2 * offset), ey - sy + (2 * offset))

    cv2.drawMarker(frame, res, (255, 0, 0), 8, 20, 4)
    cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[0] + bbox[2], bbox[1] + bbox[3]), (0, 255, 0), 2)

    cv2.imshow('image', frame)

    return res, bbox


if __name__ == "__main__":
    args = parser.parse_args()
    model_path = 'data/model.tflite'

    ws = create_connection(args.host)
    msgid = 1

    tracker = cv2.TrackerKCF_create()
    being_tracked = False
    
    cap = VideoCapture(args.ipcam)
    cap.cap.set(cv2.CAP_PROP_FRAME_WIDTH, CAMERA_WIDTH)
    cap.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, CAMERA_HEIGHT)
    cap.cap.set(cv2.CAP_PROP_FPS, 20)

    cv2.namedWindow('image')

    interpreter = load_model(model_path)
    input_details = interpreter.get_input_details()

    # Get Width and Height
    input_shape = input_details[0]['shape']
    height = input_shape[1]
    width = input_shape[2]
    input_index = input_details[0]['index']

    index = 0
    # Process Stream
    while True:
        key = cv2.waitKey(10)
        frame = cap.read()
 
        # cv2.imshow('image', frame)
        # key = cv2.waitKey(1)
        # continue

        if not being_tracked:
            image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
            image = image.resize((width, height))
            positions, conf = process_image(interpreter, image, input_index)

            if conf < 0.1:
                cv2.imshow('image', frame)
                stop()
                continue

            # detect human & initialize bounding box 
            (x, _), bbox = detect_position(positions, frame)
            
            # check if oversize
            if bbox[2] / CAMERA_WIDTH > 0.33:
                cv2.imshow('image', frame)
                stop()
                continue

            # init tracker
            tracker = cv2.TrackerKCF_create()
            ok = tracker.init(frame, bbox)
            being_tracked = True

        else:
            ok, bbox = tracker.update(frame)
            x = (bbox[0] * 2 + bbox[2]) / 2

            if not ok or index > SANITYCHECK:
                being_tracked = False
                index = 0
                continue

            index += 1
            cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[0] + bbox[2], bbox[1] + bbox[3]), (0, 255, 0), 2)

        cv2.imshow('image', frame)

        speed_left = (x / CAMERA_WIDTH) * SPEED
        speed_right = (1 - (x / CAMERA_WIDTH)) * SPEED

        print(f'{x=} -> ({speed_left},{speed_right})')

        move(speed_left, speed_right)

    cap.release()
    cleanup()
