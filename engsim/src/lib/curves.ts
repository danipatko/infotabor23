export class CubicBezier {
    // normalized by default => goes from A (0; 0) to B (1; 1)
    public ax = 0;
    public ay = 0;
    public bx = 1;
    public by = 1;

    private accuracy = 0.05; // .05 accuracy is 20 numbers
    private cached: [number, number][] = [];

    // control points define a curve
    public c1x: number;
    public c1y: number;
    public c2x: number;
    public c2y: number;
    public scaleX: number;
    public scaleY: number;

    constructor(c1x: number, c1y: number, c2x: number, c2y: number, scaleX = 1, scaleY = 1, ay = 0, by = 1) {
        this.c1x = c1x;
        this.c1y = c1y;
        this.c2x = c2x;
        this.c2y = c2y;
        this.ay = ay;
        this.by = by;
        this.scaleX = scaleX;
        this.scaleY = scaleY;

        this.cached.push([0, ay]);
        for (let t = this.accuracy; t < 1; t += this.accuracy) this.cached.push(this.getPointNormalized(t));
        this.cached.push([1, by]);
    }

    public normalize(p: [number, number]): [number, number] {
        return [p[0] / this.scaleX, p[1] / this.scaleY];
    }

    // t => [0, 1]
    public getPointNormalized(t: number): [number, number] {
        const x = (1 - t) ** 3 * this.ax + (1 - t) ** 2 * 3 * t * this.c1x + 3 * t * t * (1 - t) * this.c2x + t ** 3 * this.bx;
        const y = (1 - t) ** 3 * this.ay + (1 - t) ** 2 * 3 * t * this.c1y + 3 * t * t * (1 - t) * this.c2y + t ** 3 * this.by;
        return [x, y];
    }

    // this is a cheap solution, probably still faster than solving a cubic equation
    // for t (which involves complex numbers) then running getPoint to get Y
    private getYforXNormalized(x: number) {
        let index = 1;
        while (index < this.cached.length && x > this.cached[index][0]) index++;
        const weight = getLerpWeight(this.cached[index - 1][0], this.cached[index][0], x);
        return lerp(this.cached[index - 1][1], this.cached[index][1], weight);
    }

    private getXforYNormalized(y: number) {
        let index = 1;
        while (index < this.cached.length && y > this.cached[index][1]) index++;
        const weight = getLerpWeight(this.cached[index - 1][1], this.cached[index][1], y);
        return lerp(this.cached[index - 1][0], this.cached[index][0], weight);
    }

    public getY(x: number): number {
        return this.getYforXNormalized(Math.min(1, x / this.scaleX)) * this.scaleY;
    }

    public getX(y: number): number {
        return this.getXforYNormalized(Math.min(1, y / this.scaleY)) * this.scaleX;
    }
}

// x = a + (b - a) * t
const lerp = (a: number, b: number, t: number) => a + (b - a) * t;

// t = x - a / (b - a)
const getLerpWeight = (a: number, b: number, x: number) => (x - a) / (b - a);
