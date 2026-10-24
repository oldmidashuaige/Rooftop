using UnityEngine;

/// <summary>电台（场景静态对象）：人类 E 键交互 → GameFlow 呼叫救援（默认电台完好）。</summary>
public class RadioStation : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 2f);
    }
}
