from websocket import create_connection
from cap import VideoCapture
import argparse
import signal
import cv2

parser = argparse.ArgumentParser()
parser.add_argument('--ipcam', type=str, default="http://10.6.9.97:4747/video", help='MJPEG stream host address (droidcam server)')
parser.add_argument('--host', type=str, default="ws://roland:1111/ws", help='Roland websocket address')
parser.add_argument('--nows', type=bool, default=False, action=argparse.BooleanOptionalAction)
args = parser.parse_args()

CAMERA_WIDTH = 640
CAMERA_HEIGHT = 480
BASE_SPEED = 0.8
MAX_SPEED = 0.3
# number of frames to drop tracker
SANITYCHECK = 20

global ws

msgid = 1

def cleanup(*_):
    global args 
    if not args.nows:
        stop()
        ws.close()

    print("quit")
    exit(0)

signal.signal(signal.SIGINT, cleanup)
signal.signal(signal.SIGTERM, cleanup) 

def stop():
    global msgid
    global args

    if not args.nows:
        ws.send(f'{msgid} s')
        # print(f'{msgid} s')
        msgid += 1

def move(left, right):
    global msgid
    global args

    if not args.nows:
        ws.send(f'{msgid} m {left} {right}')
        # print(f'{msgid} m {left} {right}')
        msgid += 1

def detect(frame, detector):
    (all_corners, ids, _) = detector.detectMarkers(frame)
    if ids is not None and len(ids) > 0 and ids[0] in [0, 2, 5, 8]:
        return get_rect(all_corners[0][0])
    return None, None

def get_rect(corners):
    sum_x = 0
    (sx, sy, ex, ey) = (CAMERA_WIDTH, CAMERA_HEIGHT, -CAMERA_WIDTH, -CAMERA_HEIGHT)
    offset = 5

    for (x, y) in corners:
        sum_x += x

        sx = min(sx, x)
        sy = min(sy, y)
        ex = max(ex, x)
        ey = max(ey, y)

    return sum_x // len(corners), (int(sx - offset), int(sy - offset), int(ex - sx + (2 * offset)), int(ey - sy + (2 * offset)))

def lerp(a, b, t):
    return a + (b - a) * t

if __name__ == "__main__":
    
    if not args.nows:
        ws = create_connection(args.host)
        msgid = 1
    

    dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)
    parameters =  cv2.aruco.DetectorParameters()
    detector = cv2.aruco.ArucoDetector(dictionary, parameters)

    tracker = cv2.TrackerKCF_create()
    being_tracked = False
    
    cap = VideoCapture(args.ipcam)
    cap.cap.set(cv2.CAP_PROP_FRAME_WIDTH, CAMERA_WIDTH)
    cap.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, CAMERA_HEIGHT)
    cap.cap.set(cv2.CAP_PROP_FPS, 20)

    cv2.namedWindow('image')

    index = 0
    speed = MAX_SPEED
    relative_cube_w = 1

    # Process Stream
    while True:
        key = cv2.waitKey(10)
        frame = cap.read()

        if not being_tracked:
            x, bbox = detect(frame, detector)
            print(x, bbox)

            if x is None:
                stop()
                cv2.imshow('image', frame)
                continue

            relative_cube_w = bbox[2] / CAMERA_WIDTH
            if relative_cube_w > (1 / 3):
                stop()
                cv2.imshow('image', frame)
                continue

            speed = lerp(BASE_SPEED, MAX_SPEED, relative_cube_w * 3)
            print(speed, relative_cube_w * 3)

            # init tracker
            tracker = cv2.TrackerKCF_create()
            ok = tracker.init(frame, bbox)
            being_tracked = True

            cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[0] + bbox[2], bbox[1] + bbox[3]), (0, 0, 255), 2)

        else:
            ok, bbox = tracker.update(frame)
            x = (bbox[0] * 2 + bbox[2]) / 2

            if not ok or index > SANITYCHECK:
                being_tracked = False
                index = 0
                continue

            relative_cube_w = bbox[2] / CAMERA_WIDTH
            speed = lerp(BASE_SPEED, MAX_SPEED, relative_cube_w * 3)
            
            index += 1
            cv2.rectangle(frame, (bbox[0], bbox[1]), (bbox[0] + bbox[2], bbox[1] + bbox[3]), (0, 255, 0), 2)

        cv2.imshow('image', frame)

        speed_left = (x / CAMERA_WIDTH) * speed
        speed_right = (1 - (x / CAMERA_WIDTH)) * speed

        print(f'{x=} -> ({speed_left},{speed_right})')
        move(speed_left, speed_right)

    cap.release()
    cleanup()

