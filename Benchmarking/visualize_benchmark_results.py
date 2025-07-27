import math
import os
import json
from matplotlib.legend_handler import HandlerTuple
import matplotlib.patches
import matplotlib.pyplot as plt  # type: ignore

from enum_mappings import (
    BenchmarkingEventTypeEnumMapping,
    RenderingModeEnumMapping,
)
from typed_dicts import BenchmarkingResultsType


def visualize_benchmark_results(fp: str):
    # ignore the n first entries because of outliers
    ignore_first_n_frametimes: int = 5

    results: BenchmarkingResultsType
    with open(fp, "rt") as fs:
        results = json.load(fs)

    # pick the first timestamp
    ts_0: float = results["Timestamps"][0]  # ms

    fig, ax = plt.subplots()

    yticks_ms = [0, 1000 / 30, 1000 / 60, 1000 / 120, 1000 / 165]

    CM_TO_INCH = 0.393701

    ax.set_ylabel("frametime (ms)")
    ax.set_yticks(yticks_ms)
    ax.set_ylim(0, 50)  # we don't care about anything slower than 20 fps
    fig.set_figwidth(45 * CM_TO_INCH)
    fig.set_figheight(25 * CM_TO_INCH)
    fig.savefig(f"{os.path.basename(fp).removesuffix('.json')}.svg")

    def x_tick_formatter(t):
        return f"{t}"

    x_ticks = [0]
    x_tick_labels = [x_tick_formatter(x_ticks[0])]
    x_time_ticks_spacing = 1  # seconds
    for i in range(
        1, math.ceil((results["Timestamps"][-1] - ts_0) * 0.001 / x_time_ticks_spacing)
    ):
        x_tick = x_ticks[0] + i * x_time_ticks_spacing
        x_ticks.append(x_tick)
        x_tick_labels.append(x_tick_formatter(x_tick))

    # set 60 fps horizontal line
    ax.axhline(1000 / 60, alpha=0.2, color="red")
    sec_yaxis = ax.secondary_yaxis("right")
    sec_yaxis.set_yticks([1000 / 60])
    sec_yaxis.set_yticklabels(["60 fps"])

    ax.set_xlabel("time (s)")

    opacity_cutoff_vals = set()
    sampling_quality_factor_vals = set()
    homogeneity_tolerance_vals = set()
    lod_quality_factor_vals = set()
    new_random_rotation_axis_plots = []
    new_random_look_at_point_plots = []
    warmup_start_time: float | None = None
    frametime_measurements_start_time: float | None = None
    fct_measurements_start_time: float | None = None
    for _event in results["Events"]:
        if (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["NEW_OPCAITY_CUTOFF_VALUE"]
        ):
            opacity_cutoff_vals.add(_event["Value"])
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["NEW_SAMPLING_QUALITY_FACTOR_VALUE"]
        ):
            sampling_quality_factor_vals.add(_event["Value"])
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["NEW_HOMOGENEITY_TOLERANCE_VALUE"]
        ):
            homogeneity_tolerance_vals.add(_event["Value"])
        elif (
            _event["Type"] == BenchmarkingEventTypeEnumMapping["NEW_LOD_QUALITY_FACTOR"]
        ):
            lod_quality_factor_vals.add(_event["Value"])
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["BRICK_CACHE_WARMUP_START"]
        ):
            warmup_start_time = (_event["Timestamp"] - ts_0) * 0.001
        elif (
            _event["Type"] == BenchmarkingEventTypeEnumMapping["BRICK_CACHE_WARMUP_END"]
        ):
            if warmup_start_time is None:
                raise RuntimeError(
                    "expected BRICK_CACHE_WARMUP_START before BRICK_CACHE_WARMUP_END event."
                )
            warmup_end_time = (_event["Timestamp"] - ts_0) * 0.001
            ax.annotate(
                "",
                xy=(warmup_start_time, 1),
                xytext=(warmup_end_time, 1),
                arrowprops=dict(arrowstyle="|-|", alpha=0.4),
            )
            ax.annotate(
                "caches warmup",
                xy=((warmup_start_time + warmup_end_time) / 2, 1.5),
                ha="center",
                va="center",
                alpha=0.4,
            )
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["FRAMETIMES_MEASUREMENTS_START"]
        ):
            frametime_measurements_start_time = (_event["Timestamp"] - ts_0) * 0.001
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["FRAMETIMES_MEASUREMENTS_END"]
        ):
            if frametime_measurements_start_time is None:
                raise RuntimeError(
                    "expected FRAMETIMES_MEASUREMENTS_START event before FRAMETIMES_MEASUREMENTS_END event."
                )
            frametime_measurements_end_time = (_event["Timestamp"] - ts_0) * 0.001
            ax.annotate(
                "",
                xy=(frametime_measurements_start_time, 1),
                xytext=(frametime_measurements_end_time, 1),
                arrowprops=dict(arrowstyle="|-|", alpha=0.4),
            )
            ax.annotate(
                "frametime measurements",
                xy=(
                    (
                        frametime_measurements_start_time
                        + frametime_measurements_end_time
                    )
                    / 2,
                    1.5,
                ),
                ha="center",
                va="center",
                alpha=0.4,
            )
        elif (
            _event["Type"] == BenchmarkingEventTypeEnumMapping["FCT_MEASUREMENTS_START"]
        ):
            fct_measurements_start_time = (_event["Timestamp"] - ts_0) * 0.001
        elif _event["Type"] == BenchmarkingEventTypeEnumMapping["FCT_MEASUREMENTS_END"]:
            if fct_measurements_start_time is None:
                raise RuntimeError(
                    "expected FCT_MEASUREMENTS_START event before FCT_MEASUREMENTS_END event."
                )
            fct_measurements_end_time = (_event["Timestamp"] - ts_0) * 0.001
            ax.annotate(
                "",
                xy=(fct_measurements_start_time, 1),
                xytext=(fct_measurements_end_time, 1),
                arrowprops=dict(arrowstyle="|-|", alpha=0.4),
            )
            ax.annotate(
                "FCT measurements",
                xy=(
                    (fct_measurements_start_time + fct_measurements_end_time) / 2,
                    1.5,
                ),
                ha="center",
                va="center",
                alpha=0.4,
            )
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["NEW_RANDOM_ROTATION_AXIS"]
        ):
            new_random_rotation_axis_plots.append(
                ax.axvline(
                    (_event["Timestamp"] - ts_0) * 0.001,
                    alpha=0.25,
                    color="green",
                    linestyle="--",
                    zorder=10,
                )
            )
        elif (
            _event["Type"]
            == BenchmarkingEventTypeEnumMapping["NEW_RANDOM_LOOK_AT_POINT"]
        ):
            new_random_look_at_point_plots.append(
                ax.axvline(
                    (_event["Timestamp"] - ts_0) * 0.001,
                    alpha=0.25,
                    color="#8c7ae6",
                    linestyle="--",
                    zorder=10,
                )
            )

    assert len(opacity_cutoff_vals) == 1, "opacity cutoff value is not constant!"
    assert len(sampling_quality_factor_vals) == 1, (
        "sampling quality factor is not constant!"
    )
    assert len(homogeneity_tolerance_vals) == 1, (
        "homogeneity tolerance value is not constant!"
    )
    assert len(lod_quality_factor_vals) == 1, "LOD quality factor is not constant!"

    ax.set_xticks(x_ticks)
    ax.set_xticklabels(x_tick_labels)

    (frames_plot,) = ax.plot(
        [
            (results["Timestamps"][i] - ts_0) * 0.001
            for i in range(ignore_first_n_frametimes, len(results["Timestamps"]))
        ],
        [
            results["FrameTimes"][i]
            for i in range(ignore_first_n_frametimes, len(results["FrameTimes"]))
        ],
        color="#487eb0",
        zorder=0,
    )

    paramsBoxText: str
    if results["PipelineParameters"]["RenderingMode"] == RenderingModeEnumMapping["IC"]:
        frametimes_avg = sum(results['FrameTimes'])/len(results['FrameTimes'])
        ax.set_title("In-Core DVR Frame Times (Lower is Better)")
        paramsBoxText = "\n".join(
            [
                f"Screen Dims = {results['ScreenWidth']} x {results['ScreenHeight']}",
                f"Chunk Size = {results['DatasetMetadata']['chunk_size']}",
                f"Sampling Quality Factor = {sampling_quality_factor_vals.pop():.2f}",
                f"Opacity Cutoff = {int(opacity_cutoff_vals.pop() * 255)}",
                f"Total Nbr Bricks = {results['NbrBricks']}",
                f"Max Nbr GPU Brick Uploads Per Frame = {results['PipelineParameters']['MaxNbrGPUBrickUploadsPerFrame']}",
                f"CPU Brick Cache Size = {results['PipelineParameters']['CPUBrickCacheSizeMBs']}MBs",
                f"Max Nbr Importer Threads = {results['PipelineParameters']['MaxNbrImporterThreads']}",
                f"Bricks Loading Time To CPU Cache = {results['BricksLoadingTimeToCPUCache'] * 0.001:.2f}s",
                f"Frametime Average = {frametimes_avg:.2f}s ({int(1000/frametimes_avg)} fps)",
            ]
        )
        ax.legend(
            [
                frames_plot,
                tuple(new_random_rotation_axis_plots),
            ],
            [
                "frametimes",
                "new random rotation axis",
            ],
            loc="upper left",
            handler_map={tuple: HandlerTuple(ndivide=None)},
        )
    elif (
        results["PipelineParameters"]["RenderingMode"]
        == RenderingModeEnumMapping["OOC_PT"]
    ):
        frametimes_avg = sum(results['FrameTimes'])/len(results['FrameTimes'])
        ax.set_title(
            "Out-of-Core Virtual Memory DVR Approach Frame Times (Lower is Better)"
        )
        paramsBoxText = "\n".join(
            [
                f"Screen Dims = {results['ScreenWidth']} x {results['ScreenHeight']}",
                f"Chunk Size = {results['DatasetMetadata']['chunk_size']}",
                f"Brick Size = {results['PipelineParameters']['BrickSize']}",
                f"Sampling Quality Factor = {sampling_quality_factor_vals.pop():.2f}",
                f"Opacity Cutoff = {int(opacity_cutoff_vals.pop() * 255)}",
                f"Homogeneity Tolerance = {int(homogeneity_tolerance_vals.pop())}",
                f"Max Nbr GPU Brick Uploads Per Frame = {results['PipelineParameters']['MaxNbrGPUBrickUploadsPerFrame']}",
                f"Max Nbr Importer Threads = {results['PipelineParameters']['MaxNbrImporterThreads']}",
                f"CPU Brick Cache Size = {results['PipelineParameters']['CPUBrickCacheSizeMBs']}MBs",
                f"GPU Brick Cache Size = {results['PipelineParameters']['GPUBrickCacheSizeMBs']}MBs",
                f"Max Nbr Brick Requests Per Frame = {results['PipelineParameters']['MaxNbrBrickRequestsPerFrame']}",
                f"Max Nbr Brick Requests Per Ray = {results['PipelineParameters']['MaxNbrBrickRequestsPerRay']}",
                f"Frametime Average = {frametimes_avg:.2f}s ({int(1000/frametimes_avg)} fps)",
                f"FCT Average = {sum(results["FCTTimes"])/len(results["FCTTimes"]):.3f}s",
            ]
        )
        ax.legend(
            [
                frames_plot,
                tuple(new_random_rotation_axis_plots),
                tuple(new_random_look_at_point_plots),
            ],
            ["frametimes", "new random rotation axis", "new random look at point"],
            loc="upper left",
            handler_map={tuple: HandlerTuple(ndivide=None)},
        )
    elif (
        results["PipelineParameters"]["RenderingMode"]
        == RenderingModeEnumMapping["OOC_HYBRID"]
    ):
        frametimes_avg = sum(results['FrameTimes'])/len(results['FrameTimes'])
        ax.set_title("Out-of-Core Hybrid DVR Approach Frame Times (Lower is Better)")
        paramsBoxText = "\n".join(
            [
                f"Screen Dims = {results['ScreenWidth']} x {results['ScreenHeight']}",
                f"Chunk Size = {results['DatasetMetadata']['chunk_size']}",
                f"Brick Size = {results['PipelineParameters']['BrickSize']}",
                f"Sampling Quality Factor = {sampling_quality_factor_vals.pop():.2f}",
                f"Opacity Cutoff = {int(opacity_cutoff_vals.pop() * 255)}",
                f"Homogeneity Tolerance = {int(homogeneity_tolerance_vals.pop())}",
                f"Max Nbr GPU Brick Uploads Per Frame = {results['PipelineParameters']['MaxNbrGPUBrickUploadsPerFrame']}",
                f"Max Nbr Importer Threads = {results['PipelineParameters']['MaxNbrImporterThreads']}",
                f"CPU Brick Cache Size = {results['PipelineParameters']['CPUBrickCacheSizeMBs']}MBs",
                f"GPU Brick Cache Size = {results['PipelineParameters']['GPUBrickCacheSizeMBs']}MBs",
                f"Max Nbr Brick Requests Per Frame = {results['PipelineParameters']['MaxNbrBrickRequestsPerFrame']}",
                f"Max Nbr Brick Requests Per Ray = {results['PipelineParameters']['MaxNbrBrickRequestsPerRay']}",
                f"Octree Start Depth = {results['PipelineParameters']['OctreeStartDepth']}",
                f"Octree Max Depth = {results['PipelineParameters']['OctreeMaxDepth']}",
                f"Frametime Average = {frametimes_avg:.2f}s ({int(1000/frametimes_avg)} fps)",
                f"FCT Average = {sum(results["FCTTimes"])/len(results["FCTTimes"]):.3f}s",
            ]
        )
        ax.legend(
            [
                frames_plot,
                tuple(new_random_rotation_axis_plots),
                tuple(new_random_look_at_point_plots),
            ],
            ["frametimes", "new random rotation axis", "new random look at point"],
            loc="upper left",
            handler_map={tuple: HandlerTuple(ndivide=None)},
        )
    else:
        raise RuntimeError()

    paramsBoxProps: matplotlib.patches.Patch = {
        "boxstyle": "round",
        "alpha": 1,
        "color": "#CAD3C8",
    }
    ax.text(
        0.75,
        0.975,
        paramsBoxText,
        transform=ax.transAxes,
        fontsize=10,
        verticalalignment="top",
        bbox=paramsBoxProps,
        zorder=9999,
    )

    fig.show()


if __name__ == "__main__":
    visualize_benchmark_results(r"<path-to-benchmark-json>")
    print("done")
