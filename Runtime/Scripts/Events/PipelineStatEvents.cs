using System;
using UnityEngine;

namespace UnityCTVisualizer
{
    public class PipelineStatEvents
    {
        ///////////////////////////////////////////////////////////////////////
        /// Invoked by Models (SOs or custom classes)
        ///////////////////////////////////////////////////////////////////////
        public static Action<int> ModelNbrBrickRequestsChange;
        /// <summary>
        ///     1st param: nbr bricks used in GPU brick cache
        ///     2nd param: total nbr bricks in GPU brick cache
        ///     3rd param: brick size cubed
        /// </summary>
        public static Action<int, int, int> ModelGPUBrickCacheUsageChange;
    }
}
