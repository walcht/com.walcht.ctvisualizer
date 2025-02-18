using System;
using TMPro;

public static class ResizingUtils {
    public static float GetOptimalFontSize(TMP_Text[] texts) {
        if (texts == null || texts.Length == 0)
            throw new Exception("provided empty TMP_Text array");
        // pick a suitable candidate - simplest would be that with largest preferred width
        TMP_Text candidate = null;
        float max_preffered_width = 0;
        foreach (TMP_Text t in texts) {
            if (t.preferredWidth > max_preffered_width) {
                max_preffered_width = t.preferredWidth;
                candidate = t;
            }
        }
        if (candidate == null)
            throw new Exception("couldn't find a suitable candidate for auto font resizing");
        // force an update for the candidate TMP to get the optimum fontsize
        candidate.enableAutoSizing = true;
        candidate.ForceMeshUpdate();
        float optimum_fontsize = candidate.fontSize;
        candidate.enableAutoSizing = false;
        return optimum_fontsize;
    }
}
