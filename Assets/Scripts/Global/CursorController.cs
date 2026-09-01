using UnityEngine;

namespace Global
{
    public class CursorController : MonoBehaviour
    {
        #region Singleton

        public static CursorController Instance => _instance;

        private static CursorController _instance;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            if (_instance != this)
            {
                Destroy(this);
            }
        }

        #endregion

        #region Public Functions

        public static void EnableCursor(bool enable)
        {
            Cursor.lockState = enable ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = enable;
        }

        #endregion
    }
}