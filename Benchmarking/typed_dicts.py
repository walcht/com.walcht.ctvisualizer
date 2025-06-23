from typing import TypedDict


class SystemInfoStatsType(TypedDict):
    GPUMemorySize: int
    CPUMemorySize: int
    DeviceModel: str
    GPUModel: str
    CPUModel: str
    MaxUAVs: int
    Compressed3DTextureSupport: bool
    BatteryStatus: int
    BatteryLevel: float
    VRSSupport: bool


class EventType(TypedDict):
    Timestamp: int
    Type: int
    Value: float


class CVDSMetadataType(TypedDict):
    original_dims: list[int]
    chunk_size: int
    nbr_chunks_per_resolution_lvl: list[list[int]]
    total_nbr_chunks: list[int]
    nbr_resolution_lvls: int
    downsampling_inter: str
    color_depth: int
    force_8bit_conversion: bool
    lz4_compressed: bool
    decompressed_chunk_size_in_bytes: int
    vdhms: list[list[float]]
    octree_nrb_nodes: int
    octree_max_depth: int
    octree_smallest_subdivision: list[float]
    octree_size_in_bytes: int
    histogram_nbr_bins: int
    voxel_dims: list[float]
    euler_rotation: list[float]


class PipelineParametersType(TypedDict):
    RenderingMode: int
    BrickSize: int
    MaxNbrImporterThreads: int
    MaxNbrGPUBrickUploadsPerFrame: int
    GPUBrickCacheSizeMBs: int
    CPUBrickCacheSizeMBs: int
    InCoreMaxResolutionLvl: int
    MaxNbrBrickRequestsPerFrame: int
    MaxNbrBrickRequestsPerRay: int
    OctreeMaxDepth: int
    OctreeStartDepth: int
    BrickRequestsRandomTexSize: int


class BenchmarkingResultsType(TypedDict):
    DatasetMetadata: CVDSMetadataType
    PipelineParameters: PipelineParametersType
    BricksLoadingTimeToCPUCache: float
    BricksLoadingTimeToGPUCache: float
    NbrBricks: int
    ScreenWidth: int
    ScreenHeight: int
    SystemInfoStats: SystemInfoStatsType
    Timestamps: list[int]
    FrameTimes: list[float]
    Events: list[EventType]