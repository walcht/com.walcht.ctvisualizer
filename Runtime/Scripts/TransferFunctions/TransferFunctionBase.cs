using System;
using UnityEngine;

namespace UnityCTVisualizer
{
    public delegate void VoidHandler();

    [Serializable]
    public class ControlPoint<P, T>
    {
        public VoidHandler OnValueChange;

        [SerializeField]
        P m_Position;
        public P Position
        {
            get => m_Position;
            set
            {
                m_Position = value;
                OnValueChange?.Invoke();
            }
        }


        [SerializeField]
        T m_Value;
        public T Value
        {
            get => m_Value;
            set
            {
                m_Value = value;
                OnValueChange?.Invoke();
            }
        }

        public ControlPoint(P position, T value)
        {
            Position = position;
            Value = value;
        }
    }

    /// <summary>
    ///     
    /// </summary>
    ///
    /// <remarks>
    ///     Intended workflow is for some MonoBehaviour to call TryUpdateColorLookupTex within its Update method:
    ///
    ///     <code>
    ///     void Update()
    ///     {
    ///         // ...
    ///         m_TransferFunction.TryUpdateColorLookupTex();
    ///         // ...
    ///     }
    ///     </code>
    ///     
    ///     For any Shader requiring the color lookup texture, the texture should be retrieve initially using
    ///     GetColorLookupTex.
    ///     
    /// </remarks>
    public abstract class ITransferFunction
    {
        protected readonly Texture2D m_ColorLookupTex;
        protected readonly byte[] m_RawTexData;
        protected bool m_Dirty = true;


        public ITransferFunction(int texWidth, int texHeight)
        {
            m_ColorLookupTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, mipChain: false,
                linear: true);
            m_ColorLookupTex.wrapModeU = TextureWrapMode.Clamp;
            m_ColorLookupTex.wrapModeV = TextureWrapMode.Clamp;
            m_RawTexData = new byte[texWidth * texHeight * 4];
        }


        /// <summary>
        ///     Tries to update the 2D color lookup texture if the internal dirty flag is set.
        ///     Does nothing otherwise.
        /// </summary>
        public void TryUpdateColorLookupTex()
        {
            if (m_Dirty)
            {
                GenerateColorLookupTexData();
                m_ColorLookupTex.SetPixelData(m_RawTexData, 0);
                m_ColorLookupTex.Apply();
                m_Dirty = false;
            }
        }


        /// <summary>
        ///     Gets the 2D color lookup texture managed by this transfer function.
        /// </summary>
        /// 
        /// <returns>
        ///     2D color lookup texture which is sampled using densities.
        /// </returns>
        public Texture GetColorLookupTex() => m_ColorLookupTex;


        /// <summary>
        ///     Generates raw color lookup data (assuming an RGBA32 texture format) and assigns it to the
        ///     pre-allocated array <code>m_RawTexData</code>.
        /// </summary>
        protected abstract void GenerateColorLookupTexData();


        /// <summary>
        ///     Serialized the transfer function data to persistent memory.
        /// </summary>
        public abstract void Serialize();


        /// <summary>
        ///     Deserializes the transfer function data from a provided JSON filename.
        /// </summary>
        /// 
        /// <param name="filename">
        ///     Filename of the transfer function JSON data to deserialize.
        /// </param>
        public abstract void Deserialize(string filename);
    }
}
