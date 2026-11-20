// Host component for the software synth (must live in a file of the same name).
using UnityEngine;

public class QFAudioSynth : MonoBehaviour
{
    void OnAudioFilterRead(float[] data, int channels) { A.Render(data, channels); }
    void Update() { A.Tick(Time.unscaledDeltaTime); }
}
