using UnityEngine;
using UnityEngine.InputSystem;

namespace Parasite
{
    /// <summary>Code-defined input actions (keyboard/mouse + gamepad). No asset wiring required.</summary>
    public static class GameInput
    {
        static InputActionMap map;
        public static InputAction Move, Look, Sprint, Jump, Interact, Leave, Split, Switch, Pause, Ability, PresentationEnding;
        static bool presentationPending, presentationArmed = true;

        /// <summary>Set by automated tests / autoplay to drive input without devices.</summary>
        public static bool Simulated;
        public static Vector2 SimMove;
        public static bool SimSprint, SimJump, SimInteract, SimLeave, SimSplit, SimSwitch, SimAbility;

        public static float MouseSensitivity = 0.12f;
        public static float StickSensitivity = 160f;

        public static void Ensure()
        {
            if (map != null) return;
            map = new InputActionMap("Parasite");
            Move = map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");
            Look = map.AddAction("Look", InputActionType.Value);
            Look.AddBinding("<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=8,y=8)");
            Sprint = map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");
            Jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");
            Interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonWest");
            Leave = map.AddAction("Leave", InputActionType.Button, "<Keyboard>/q");
            Leave.AddBinding("<Gamepad>/buttonEast");
            Split = map.AddAction("Split", InputActionType.Button, "<Keyboard>/v");
            Split.AddBinding("<Gamepad>/buttonNorth");
            Switch = map.AddAction("Switch", InputActionType.Button, "<Keyboard>/tab");
            Switch.AddBinding("<Gamepad>/rightShoulder");
            Ability = map.AddAction("Ability", InputActionType.Button, "<Keyboard>/f");
            Ability.AddBinding("<Gamepad>/leftShoulder");
            Pause = map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");
            PresentationEnding = map.AddAction("PresentationEnding", InputActionType.Button);
            PresentationEnding.AddCompositeBinding("ButtonWithOneModifier(modifiersOrder=2)")
                .With("Modifier", "<Keyboard>/leftCtrl").With("Button", "<Keyboard>/m");
            PresentationEnding.AddCompositeBinding("ButtonWithOneModifier(modifiersOrder=2)")
                .With("Modifier", "<Keyboard>/rightCtrl").With("Button", "<Keyboard>/m");
            PresentationEnding.performed += OnPresentationEnding;
            map.Enable();
        }

        static void OnPresentationEnding(InputAction.CallbackContext context)
        {
            if (!presentationArmed) return;
            // Buffer the performed event: a quick press/release between game frames must survive.
            presentationPending = true;
            presentationArmed = false;
        }

        public static Vector2 MoveValue
        {
            get { Ensure(); return Simulated ? SimMove : Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f); }
        }

        /// <summary>Camera look delta in degrees for this frame.</summary>
        public static Vector2 LookDelta
        {
            get
            {
                Ensure();
                if (Simulated) return Vector2.zero;
                var d = Look.ReadValue<Vector2>();
                bool mouse = Look.activeControl != null && Look.activeControl.device is Mouse;
                return mouse ? d * MouseSensitivity : d * (StickSensitivity / 8f) * Time.unscaledDeltaTime;
            }
        }

        public static bool SprintHeld { get { Ensure(); return Simulated ? SimSprint : Sprint.IsPressed(); } }
        public static bool JumpPressed { get { Ensure(); return Simulated ? SimJump : Jump.WasPressedThisFrame(); } }
        public static bool InteractPressed { get { Ensure(); return Simulated ? SimInteract : Interact.WasPressedThisFrame(); } }
        public static bool LeavePressed { get { Ensure(); return Simulated ? SimLeave : Leave.WasPressedThisFrame(); } }
        public static bool SplitPressed { get { Ensure(); return Simulated ? SimSplit : Split.WasPressedThisFrame(); } }
        public static bool SwitchPressed { get { Ensure(); return Simulated ? SimSwitch : Switch.WasPressedThisFrame(); } }
        public static bool AbilityPressed { get { Ensure(); return Simulated ? SimAbility : Ability.WasPressedThisFrame(); } }
        public static bool PausePressed { get { Ensure(); return Pause.WasPressedThisFrame(); } }
        /// <summary>One buffered Ctrl+M request, either key order; release all chord keys to rearm.</summary>
        public static bool PresentationEndingPressed
        {
            get
            {
                Ensure();
                if (!PresentationEnding.enabled) PresentationEnding.Enable();
                bool anyHeld = false, chordHeld = false;
                foreach (var device in InputSystem.devices)
                {
                    if (device is not Keyboard keyboard) continue;
                    bool modifier = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                    bool letter = keyboard.mKey.isPressed;
                    anyHeld |= modifier || letter;
                    chordHeld |= modifier && letter;
                }
                if (!anyHeld) presentationArmed = true;
                // Fallback covers a keyboard already held when the action was enabled.
                if (chordHeld && presentationArmed) { presentationPending = true; presentationArmed = false; }
                bool pressed = presentationPending;
                presentationPending = false;
                return pressed;
            }
        }
    }
}
