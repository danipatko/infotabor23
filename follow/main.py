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
SPEED = 0.8

global ws

def cleanup(*args):
    stop()
    ws.close()
    print("quit")
    exit(0)

def stop():
    ws.send('s')
    ws.recv()

def move(left, right):
    ws.send(f'm {left} {right}')
    ws.recv()

signal.signal(signal.SIGINT, cleanup)

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

    # totally 17 points, only legs are relevant: 9 -> 17
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

    confidence = confidence / len(points)
    return positions, confidence


def get_position(positions, frame):
    sum_x = 0
    sum_y = 0

    for pos in positions:
        pos_x, pos_y, offset_x, offset_y = pos

        # Calculating the x and y coordinates
        x = int(pos_x / 8 * CAMERA_WIDTH + offset_x)
        y = int(pos_y / 8 * CAMERA_HEIGHT + offset_y)

        sum_x += x
        sum_y += y
    
    res = (sum_x // len(positions), sum_y // len(positions))
    
    cv2.drawMarker(frame, res, (255, 0, 0), 8, 20, 4)
    cv2.imshow('image', frame)

    return res

if __name__ == "__main__":
    args = parser.parse_args()
    model_path = 'data/model.tflite'

    ws = create_connection(args.host)

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

    # Get input index
    input_index = input_details[0]['index']

    # Process Stream
    while True:
        frame = cap.read()

        # cv2.imshow('image', frame)
        # key = cv2.waitKey(1)
        # continue

        image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        image = image.resize((width, height))

        positions, conf = process_image(interpreter, image, input_index)

        key = cv2.waitKey(10)
        # why does it always return -1
        # print(key)
        # if key == ord(' '):
        #     enabled = not enabled
        # if key == ord('q') or key == 27:  # esc
        #     break

        if conf < 0.1:
            cv2.imshow('image', frame)
            # print('Not confident enough')
            continue

        (x, y) = get_position(positions, frame)

        speed_left = (x / CAMERA_WIDTH) * SPEED
        speed_right = (1 - (x / CAMERA_WIDTH)) * SPEED

        print(f'{x=} {y=} -> ({speed_left},{speed_right})')

        # move(speed_left, speed_right)

    cap.release()
    cleanup()
