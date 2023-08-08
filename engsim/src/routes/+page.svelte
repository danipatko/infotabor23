<script lang="ts">
    import { defaultInputs, getInputs, type Inputs } from "$lib/gamepad";
    import Gauge, { rpmGaugeConf } from "$lib/svgutil";
    import Engine from "$lib/engine";
    import { onMount } from "svelte";
    import { clamp } from "$lib/util";

    let engine = new Engine();
    let controller: Gamepad | null = null;
    let inputs: Inputs = defaultInputs;

    const gears = (g: number): string => (g == -1 ? "R" : g == 0 ? "N" : g.toString());

    let velocity = 0;
    let rpm = 800;
    let realClutch = 800;

    let throttle = 0.5;
    let gear = 0;
    let clutch = 0;
    let brake = false;

    // DISPLAY
    let speedGauge = new Gauge();
    let speedGaugeSVG = "";
    let rpmGauge = new Gauge(rpmGaugeConf);
    let rpmGaugeSVG = "";

    let gearDisp = "N";
    let speedDisp = (0).toFixed(2);

    onMount(() => {
        window.addEventListener("gamepadconnected", (e) => (controller ||= e.gamepad));
        window.addEventListener("gamepaddisconnected", (e) => controller?.id === e.gamepad.id && (controller = null));

        setInterval(update, 10);
    });

    function update() {
        if (controller != null) {
            inputs = getInputs(controller);
            if (inputs.buttonA) gear = engine.changeGear(gear + 1);
            if (inputs.buttonX) gear = engine.changeGear(gear - 1);
            gearDisp = gears(gear);

            throttle = inputs.rightTrigger;
            clutch = inputs.leftTrigger;
            brake = inputs.leftBump;
        }

        engine.currentGear = gear;
        const [c, r, v] = engine.update(throttle, clutch, brake);
        realClutch = c;
        rpm = r;
        velocity = v;

        speedDisp = engine.nVelocity.toFixed(2);

        speedGauge.gauge = Math.abs(engine.nVelocity);
        speedGaugeSVG = speedGauge.SVG;

        rpmGauge.gauge = engine.nRPM;
        rpmGaugeSVG = rpmGauge.SVG;

        send();
    }

    let webSocket: WebSocket | null = null;
    let url = "ws://roland:1111/ws";
    let id = 1;

    onMount(() => reconnect());

    function reconnect() {
        webSocket = new WebSocket(url);
    }

    function send() {
        let right = engine.nVelocity,
            left = engine.nVelocity;

        if (inputs.axisX > 0) right *= 1 - inputs.axisX;
        else left *= 1 + inputs.axisX;

        if (webSocket?.readyState == WebSocket.OPEN) {
            // console.log(`m ${Math.floor(left * 100)} ${Math.floor(right * 100)}`);
            // webSocket.send(`m ${Math.floor(left * 100)} ${Math.floor(right * 100)}`);

            // console.log(`${id++} m ${left} ${right}`);
            // webSocket.send(`${id++} m ${left} ${right}`);

            console.log(`m ${left} ${right}`);
            webSocket.send(`m ${left} ${right}`);
        }
    }
</script>

<div>
    <input type="text" bind:value={url} on:change={reconnect} />
</div>
<div class="center">
    <h2>{controller == null ? "Waiting for controller input" : controller.id}</h2>
    <div class="dash-container">
        <div class="gauge-container">
            {@html rpmGaugeSVG}
        </div>
        <div class="dash-center">
            <div class="div1">{gearDisp}</div>
            <div class="div2">{new Date().toISOString().split("T")[1].substring(0, 5)}</div>
            <div class="div3">{speedDisp} <span id="kph">unit</span></div>
            <div class="div4">{" "}</div>
            <div class="div5">{" "}</div>
            <div class="div6">{" "}</div>
        </div>
        <div class="gauge-container">
            {@html speedGaugeSVG}
        </div>
    </div>
</div>

<style>
    @import url("https://fonts.googleapis.com/css2?family=Squada+One&display=swap");
    @import url("https://fonts.cdnfonts.com/css/seven-segment");

    :global(svg > line) {
        filter: drop-shadow(0px 0px 4px rgb(255 0 0 / 0.8));
    }

    * {
        font-family: monospace;
    }

    :global(body, html) {
        background-color: black;
        color: whitesmoke;
    }

    .dash-center {
        background: rgba(195, 44, 0, 1);
        background: radial-gradient(circle, rgba(225, 70, 0, 1) 0%, rgba(195, 44, 0, 1) 100%);
        height: 200px;
        width: 150px;
        border-radius: 5px;
        filter: drop-shadow(0px 0px 8px rgb(255 100 0 / 0.5));
        padding: 10px;
        color: black;

        display: grid;
        grid-template-columns: repeat(3, 1fr);
        grid-template-rows: repeat(5, 1fr);
        grid-column-gap: 2px;
        grid-row-gap: 2px;
    }

    .dash-center div,
    .dash-center span {
        font-family: "Seven Segment", sans-serif;
        font-weight: 600;
        font-size: 1.8rem;
    }

    #kph {
        font-size: 1rem !important;
        padding-left: 10px;
    }

    .center {
        display: flex;
        flex-direction: column;
        justify-content: center;
        gap: 2rem;
        align-items: center;
        height: 95vh;
    }

    .dash-container {
        display: flex;
        justify-content: center;
        gap: 2rem;
        align-items: center;
    }

    .div1 {
        grid-area: 1 / 1 / 2 / 2;
    }
    .div2 {
        grid-area: 1 / 2 / 2 / 4;
        display: flex;
        justify-content: end;
    }
    .div3 {
        grid-area: 2 / 1 / 5 / 4;
        display: flex;
        justify-content: center;
        align-items: center;
        font-size: 3rem !important;
    }
    .div4 {
        grid-area: 5 / 1 / 6 / 2;
    }
    .div5 {
        grid-area: 5 / 2 / 6 / 3;
    }
    .div6 {
        grid-area: 5 / 3 / 6 / 4;
    }
</style>
