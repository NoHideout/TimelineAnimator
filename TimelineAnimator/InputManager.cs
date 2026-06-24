using Dalamud.Game.ClientState.Keys;
using System.Runtime.InteropServices;

namespace TimelineAnimator
{
    public class InputManager
    {
        private bool wasPlaybackKeyPressed = false;
        private bool wasAddItemKeyPressed = false;

        [DllImport("user32.dll")] //TODO should handle later through CameraService or keystate
        private static extern short GetAsyncKeyState(int vKey);

        private bool IsKeyPressed(VirtualKey key)
        {
            if (key == VirtualKey.NO_KEY) return false;
            if (Services.KeyState[key]) return true;
            const int KEY_PRESSED = 0x8000;
            return (GetAsyncKeyState((int)key) & KEY_PRESSED) != 0;
        }

        public bool IsModifierHeld
        {
            get
            {
                if (Services.Configuration.ModifierKey == VirtualKey.NO_KEY) return true;
                return IsKeyPressed(Services.Configuration.ModifierKey);
            }
        }

        public bool shouldBlockGameInput
        {
            get
            {
                if (Services.Configuration.ModifierKey == VirtualKey.NO_KEY) return false;
                return IsKeyPressed(Services.Configuration.ModifierKey);
            }
        }

        public bool IsTogglePlaybackPressed()
        {
            if (Services.Configuration.TogglePlaybackKey == VirtualKey.NO_KEY) return false;
            if (Services.Configuration.ModifierKey != VirtualKey.NO_KEY && !IsModifierHeld)
            {
                wasPlaybackKeyPressed = false;
                return false;
            }

            bool isPressed = IsKeyPressed(Services.Configuration.TogglePlaybackKey);
            bool result = isPressed && !wasPlaybackKeyPressed;
            wasPlaybackKeyPressed = isPressed;
            return result;
        }

        public bool IsAddItemPressed()
        {
            if (Services.Configuration.AddItemKey == VirtualKey.NO_KEY) return false;

            if (Services.Configuration.ModifierKey != VirtualKey.NO_KEY && !IsModifierHeld)
            {
                wasAddItemKeyPressed = false;
                return false;
            }

            bool isPressed = IsKeyPressed(Services.Configuration.AddItemKey);
            bool result = isPressed && !wasAddItemKeyPressed;
            wasAddItemKeyPressed = isPressed;
            return result;
        }
    }
}