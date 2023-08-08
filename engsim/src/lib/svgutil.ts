import { lerp, lerpPoint, rad, type Point } from "./util";

export type StyleRules = { [key: number]: { style: string; width: number; label: null | ((i: number) => string) } };

interface IGauge {
    width: number;
    pad: number;
    kr: number;
    start?: number | undefined;
    cap?: string | undefined;
    steps: number;
    style: StyleRules;
    labelStyle: string;
}

// simple speedometer
export const speedometer: IGauge = {
    width: 400,
    pad: 5,
    kr: 225,
    steps: 135,
    style: {
        1: { style: "stroke:red;stroke-width:1", width: 0.05, label: null },
        5: { style: "stroke:red;stroke-width:3", width: 0.07, label: null },
        10: { style: "stroke:white;stroke-width:4", width: 0.1, label: (i: number) => `${(i + 1) * 2}` },
    },
    labelStyle: "font-family:'Squada One',cursive;font-size:1.2rem;font-weight: bold;",
    cap: "km/h",
};

export const rpmGaugeConf: IGauge = {
    ...speedometer,
    kr: 225,
    steps: 10 * 7,
    // start: rad(150),
    style: {
        1: { style: "stroke:red;stroke-width:2", width: 0.05, label: null },
        10: { style: "stroke:white;stroke-width:4", width: 0.1, label: (i: number) => `${(i + 1) / 10}` },
    },
    cap: "×1000/min",
};

export default class Gauge {
    private width: number;
    private height: number;
    private padding: number;
    private radius: number;
    private cx: number;
    private cy: number;

    private style: StyleRules;
    private labelStyle: string;
    private gaugeStyle = "stroke:red;stroke-width:2";
    private capStyle = "font-family:'Squada One',cursive;font-size:1rem;font-weight: bold;";

    private cap?: string | undefined;

    private get center(): Point {
        return [this.cx, this.cy];
    }

    private totalSteps: number;
    // segment on circumference
    private kr: number;
    private startAng: number;
    private stepAng: number;

    private backgroundSVG = "";
    private gaugeSVG = "";

    constructor({ width, pad, kr, start, steps, style, labelStyle, cap }: IGauge = speedometer) {
        this.width = width;
        this.cx = width / 2;
        this.height = this.width;
        this.cy = this.height / 2;
        this.padding = pad;
        this.radius = this.width / 2 - this.padding;

        this.totalSteps = steps;
        this.kr = rad(Math.min(360, kr));
        this.startAng = start !== undefined ? start : this.kr > Math.PI ? Math.PI - (this.kr - Math.PI) / 2 : -(Math.PI - (Math.PI - this.kr) / 2);
        this.stepAng = this.kr / (this.totalSteps - 1);

        this.cap = cap;
        this.style = style;
        this.labelStyle = labelStyle;

        // background
        const keys = (Object.keys(this.style) as any as (keyof StyleRules)[]).sort((a, b) => b - a);

        if (this.cap !== undefined) {
            const p = lerpPoint(this.center, [this.cx, 0], 0.5);
            this.backgroundSVG += `<text x="${p[0]}" y="${p[1]}" fill="gray" dominant-baseline="middle" text-anchor="middle" style="${this.capStyle}">${this.cap}</text>`;
        }

        for (let i = 0; i < this.totalSteps; i++) {
            const s = this.getStyle(i, keys),
                p = this.getPoint(i),
                p2 = lerpPoint(p, this.center, s.width);

            this.backgroundSVG += `<line x1="${p[0]}" y1="${p[1]}" x2="${p2[0]}" y2="${p2[1]}" style="${s.style}" />`;
            if (s.label != null) {
                const pt = lerpPoint(p, this.center, s.width + 0.1);
                this.backgroundSVG += `<text x="${pt[0]}" y="${pt[1]}" fill="white" dominant-baseline="middle" text-anchor="middle" style="${
                    this.labelStyle
                }">${s.label(i)}</text>`;
            }
        }
    }

    private getPoint(step: number): Point {
        const angle = this.startAng + step * this.stepAng;
        return [this.cx + Math.cos(angle) * this.radius, this.cy + Math.sin(angle) * this.radius];
    }

    private getStyle(i: number, keys: (keyof StyleRules)[]) {
        for (const k of keys) {
            if ((i + 1) % k == 0) return this.style[k];
        }
        return this.style["1"];
    }

    public get SVG() {
        return `<svg height="${this.height}" width="${this.width}">${this.backgroundSVG}${this.gaugeSVG}</svg> `;
    }

    public set gauge(t: number) {
        const p = lerpPoint(this.center, this.getPoint(t * this.totalSteps), 0.8);
        this.gaugeSVG = `<line x1="${this.cx}" y1="${this.cy}" x2="${p[0]}" y2="${p[1]}" style="${this.gaugeStyle}" />`;
    }
}
