import { CubicBezier } from "./curves";
import { kphToMps, lerp, mpsToKph, RPMToRads } from "./util";

export default class Engine {
    // CONSTANTS
    private gearRatios = [-2.9, 0, 2.66, 1.78, 1.3, 1, 0.74]; // R, N, 1, 2, 3, 4, 5
    private differentialRatio = 3.42;
    private wheelRadius = 0.34; // in meters

    private rpmMax = 7000;
    private rpmMin = 800;
    public maxSpeed = 0;

    private torqueCurve = new CubicBezier(0.33, 0.2, 0.66, 0.5);
    // x: clutch position | y: effective engagement
    private clutchCurve = new CubicBezier(1, 0, 0.65, 0);
    private brakeForceCurve = new CubicBezier(1, 0.09, 1, 0.07);
    private loadCurve = new CubicBezier(0, 0.67, 0, 0.67);

    // [-1, 0, ..5]
    public currentGear = 1;
    private get currentGearRatio() {
        return this.gearRatios[this.currentGear + 1];
    }

    public crankshaftRPM = this.rpmMin;
    public drivetrainRPM = 0;

    public targetRPM = this.rpmMin;
    public velocity = 0;

    public throttle = 0; // [0, 1]
    public clutch = 0; // [0 - disengaged, 1 - engaged]

    constructor() {
        this.torqueCurve.by = 0;
        this.maxSpeed = this.getSpeed(this.rpmMax / this.gearRatios[this.gearRatios.length - 1] / this.differentialRatio);
    }

    public changeGear(gear: number) {
        if (!this.clutchDisengaged) return this.currentGear;

        if (gear > this.currentGear) this.currentGear = Math.min(this.currentGear + 1, this.gearRatios.length - 2);
        else if (gear < this.currentGear) this.currentGear = Math.max(this.currentGear - 1, -1);

        return this.currentGear;
    }

    public get clutchDisengaged() {
        return this.clutch < 0.05;
    }

    // normalized velocity
    public get nVelocity() {
        return this.velocity / this.maxSpeed;
    }

    public get nRPM() {
        return this.crankshaftRPM / this.rpmMax;
    }

    private getOutput(rpm: number) {
        return this.currentGearRatio == 0 ? 0 : rpm / (this.currentGearRatio * this.differentialRatio);
    }

    private getSpeed(rpm: number) {
        return mpsToKph(RPMToRads(rpm) * this.wheelRadius);
    }

    private rpmFromSpeed(speed: number) {
        const rotationRate = kphToMps(speed) / this.wheelRadius;
        return (rotationRate * this.currentGearRatio * this.differentialRatio * 60) / (2 * Math.PI);
    }

    public update(throttle: number, clutch: number = 0, brake: boolean) {
        this.throttle = throttle;
        this.clutch = 1 - this.clutchCurve.getY(clutch);
        this.targetRPM = lerp(this.rpmMin, this.rpmMax, this.throttle);

        // default deceleration rate (in neutral)
        let rate = 0.03;

        if (this.targetRPM > this.crankshaftRPM) {
            // in neutral
            rate = this.torqueCurve.getY(this.crankshaftRPM / this.rpmMax);
            // calculate load
            rate *= this.currentGear == 0 ? 0.15 : lerp(0.15, 0, this.loadCurve.getY(this.crankshaftRPM / this.rpmMax) * this.clutch);
        }

        // independent crankshaft and drivetrain RPMs
        const crankshaftRPM = lerp(this.crankshaftRPM, this.targetRPM, rate);
        const drivetrainRPM = this.rpmFromSpeed(this.velocity);

        if (this.currentGear != 0) {
            if (crankshaftRPM * this.clutch >= drivetrainRPM && !brake) {
                // accelarating
                const targ = lerp(crankshaftRPM, drivetrainRPM, this.clutch);
                this.crankshaftRPM = lerp(crankshaftRPM, targ, 0.1);
                this.drivetrainRPM = lerp(this.drivetrainRPM, this.crankshaftRPM * this.clutch, 0.2);

                this.velocity = this.getSpeed(this.getOutput(this.drivetrainRPM));
            } else {
                // decelerating
                const targ = lerp(0.05, this.brakeForceCurve.getY(this.crankshaftRPM / this.rpmMax), this.clutch);
                this.drivetrainRPM = brake ? drivetrainRPM * 0.96 : drivetrainRPM * lerp(0.99999, 0.99, targ);
                this.crankshaftRPM = lerp(crankshaftRPM, lerp(crankshaftRPM, drivetrainRPM, this.clutch), 0.15);

                this.velocity = this.getSpeed(this.getOutput(this.drivetrainRPM));
            }
        } else {
            this.crankshaftRPM = crankshaftRPM;
            this.velocity *= brake ? 0.96 : 0.999;
            this.drivetrainRPM = this.rpmFromSpeed(this.velocity);
        }

        return [this.clutch, this.crankshaftRPM, this.velocity];
    }
}
