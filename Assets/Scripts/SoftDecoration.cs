using UnityEngine;

// Visual-only cloth or distant scenery is not a rigid wall.
// Furniture collision generation skips it; authored safety volumes remain separate.
public sealed class SoftDecoration : MonoBehaviour { }
