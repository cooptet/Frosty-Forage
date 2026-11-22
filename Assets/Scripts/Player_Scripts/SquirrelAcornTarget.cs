using UnityEngine;

public sealed class SquirrelAcornTarget : MonoBehaviour {
    public bool TryCollect() {
        if (!isActiveAndEnabled) return false;

        gameObject.SetActive(false);
        return true;
    }

    private void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1f, 0.65f, 0.24f);
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}