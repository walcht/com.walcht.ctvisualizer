using System;
using UnityEngine;

namespace UnityCTVisualizer
{
    public static class TransferFunctionEvents
    {
        ///////////////////////////////////////////////////////////////////////
        /// Invoked by Models (SOs or custom classes)
        ///////////////////////////////////////////////////////////////////////
        public static Action<int, ControlPoint<float, Color>> ModelTF1DColorControlAddition;
        public static Action<int> ModelTF1DColorControlRemoval;
        public static Action<int, ControlPoint<float, float>> ModelTF1DAlphaControlAddition;
        public static Action<int> ModelTF1DAlphaControlRemoval;


        ///////////////////////////////////////////////////////////////////////
        /// Invoked by Views (UIs)
        ///////////////////////////////////////////////////////////////////////
        public static Action<ControlPoint<float, Color>> ViewTF1DColorControlAddition;
        public static Action<int> ViewTF1DColorControlRemoval;
        public static Action<ControlPoint<float, float>> ViewTF1DAlphaControlAddition;
        public static Action<int> ViewTF1DAlphaControlRemoval;
    }
}
