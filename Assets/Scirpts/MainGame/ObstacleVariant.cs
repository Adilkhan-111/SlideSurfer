using UnityEngine;

/// <summary>
/// The single obstacle prefab is spawned both in the middle of the slide and on its sides.
/// This picks the look that fits where it ended up: the "wet floor" signs in the middle,
/// the steel barrier on the sides. The spawner moves the obstacle after enabling it, so check every frame.
/// </summary>
public class ObstacleVariant : MonoBehaviour
{
    [SerializeField] private GameObject centerVisual;
    [SerializeField] private GameObject sideVisual;
    [Tooltip("Obstacles closer than this to the middle of the slide (on X) use the center look")]
    [SerializeField] private float centerHalfWidth = 0.6f;

    private int current = -1;

    private void Update()
    {
        int wanted = Mathf.Abs(transform.position.x) < centerHalfWidth ? 0 : 1;
        if (wanted == current) return;
        current = wanted;
        if (centerVisual != null) centerVisual.SetActive(wanted == 0);
        if (sideVisual != null) sideVisual.SetActive(wanted == 1);
    }

    private void OnDisable()
    {
        current = -1;
    }
}
