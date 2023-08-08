<script lang="ts">
    export let name: string;
    export let value: number;
    export let min: number;
    export let max: number;
</script>

<div class="m">
    <div>{name}</div>
    <meter {min} {max} {value} />
</div>

<style>
    .m {
        width: fit-content;
        display: flex;
        justify-content: center;
        gap: 1em;
    }

    meter {
        -moz-appearance: none;
        appearance: none;
        background: none;
        background-color: #ccc;
        height: 5px;
    }

    meter::-moz-meter-bar {
        -moz-appearance: none;
        appearance: none;
        background: none;
        /* background-image: linear-gradient(90deg, #000, #000);
        background-size: 100% 100%; */
        background-color: red;
    }
</style>
