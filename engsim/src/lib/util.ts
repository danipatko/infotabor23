export type Point = [number, number];

export const deg = (rad: number): number => rad * (180 / Math.PI);

export const rad = (deg: number): number => deg * (Math.PI / 180);

export const lerpPoint = (a: Point, b: Point, t: number): Point => [lerp(a[0], b[0], t), lerp(a[1], b[1], t)];

// x = a + (b - a) * t
export const lerp = (a: number, b: number, t: number) => a + (b - a) * t;

// t = (x - a) / (b - a)
export const getLerpWeight = (a: number, b: number, x: number) => (x - a) / (b - a);

export const radsToRPM = (rads: number) => (rads * 60) / (2 * Math.PI);

export const RPMToRads = (rpm: number) => (rpm / 60) * (2 * Math.PI);

export const clamp = (min: number, max: number, val: number) => Math.min(max, Math.max(min, val));

export const kphToMps = (kph: number) => (kph * 1000) / 3600;

export const mpsToKph = (mps: number) => (mps * 3600) / 1000;
