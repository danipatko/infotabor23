export interface Inputs {
    leftTrigger: number;
    rightTrigger: number;
    axisX: number;
    axisY: number;
    buttonA: boolean;
    buttonX: boolean;
    leftBump: boolean;
}

export let defaultInputs: Inputs = {
    leftTrigger: 0,
    rightTrigger: 0,
    axisX: 0,
    axisY: 0,
    buttonA: false,
    buttonX: false,
    leftBump: false,
};

let A = false,
    X = false;

// tested on '045e-0b12-Microsoft Xbox Series S|X Controller'
export function getInputs(controller: Gamepad): Inputs {
    // only release
    const buttons = { buttonA: A && !controller.buttons[0].pressed, buttonX: X && !controller.buttons[2].pressed };
    A = controller.buttons[0].pressed;
    X = controller.buttons[2].pressed;
    return {
        axisX: controller.axes[0],
        axisY: controller.axes[1],
        leftBump: controller.buttons[4].pressed,
        ...buttons,
        // [-1,1] => [0,1]
        leftTrigger: (controller.axes[2] + 1) / 2,
        rightTrigger: (controller.axes[5] + 1) / 2,
    };
}
