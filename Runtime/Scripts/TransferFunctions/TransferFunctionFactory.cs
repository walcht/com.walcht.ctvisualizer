using UnityCTVisualizer;
using System;

public static class TransferFunctionFactory
{
    public static ITransferFunction Create(TF tf)
    {
        switch (tf)
        {
            case TF.TF1D:
            return new TransferFunction1D();

            default:
            throw new Exception(tf.ToString());
        }
    }
}
