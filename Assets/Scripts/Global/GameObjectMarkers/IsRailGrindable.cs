using UnityEngine;
using UnityEngine.Splines;

namespace Global.GameObjectMarkers
{
    public class IsRailGrindable : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SplineContainer _splineContainer;

        public SplineContainer SplineContainer => _splineContainer;
    }
}