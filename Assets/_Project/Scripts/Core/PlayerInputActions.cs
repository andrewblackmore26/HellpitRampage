// Hand-authored wrapper for PlayerInput.inputactions.
// Mirrors what Unity's "Generate C# Class" produces (InputActionAsset.FromJson + per-action getters).
// If Unity regenerates this file (importer's generateWrapperCode flipped on), the embedded JSON below
// will be re-emitted from the .inputactions source — keep the two byte-identical when editing by hand.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace HellpitRampage.Core
{
    public partial class @PlayerInputActions : IInputActionCollection2, IDisposable
    {
        public InputActionAsset asset { get; }

        public @PlayerInputActions()
        {
            asset = InputActionAsset.FromJson(@"{
    ""name"": ""PlayerInput"",
    ""maps"": [
        {
            ""name"": ""Player"",
            ""id"": ""df715a9e-ba97-483a-b42c-c8d13b7a8442"",
            ""actions"": [
                {
                    ""name"": ""Movement"",
                    ""type"": ""Value"",
                    ""id"": ""d0b00fcc-1ff4-43e3-adce-5a3e0f06d2d0"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""ActiveAbility"",
                    ""type"": ""Button"",
                    ""id"": ""9e4cf816-c775-4b63-a757-9e88a11a0c71"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Pause"",
                    ""type"": ""Button"",
                    ""id"": ""4dfa4a7c-acc7-4ca6-929d-ca5f4677b828"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""WASD"",
                    ""id"": ""effb1487-2671-4c54-8a4d-ea7ea191f018"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""09f923d6-b641-4b7b-8797-5b7875f9d988"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""efcb014b-7fc1-4309-95b9-0bad91859f1e"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""87d905ac-ef85-426e-b6c2-886b78eab3b1"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""e93e5f15-d85d-4485-ad93-b2ed2d5efdee"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""83adc67d-b5d1-4c4a-bdc5-9b79240a6547"",
                    ""path"": ""<Gamepad>/leftStick"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""080d7dbb-0eb3-4004-9328-9d553320022e"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""ActiveAbility"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""cb8e4025-8080-4332-92c5-790f17b93297"",
                    ""path"": ""<Gamepad>/buttonSouth"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""ActiveAbility"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""789885e2-3bad-4ff7-8d47-76166a0f2905"",
                    ""path"": ""<Keyboard>/escape"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Keyboard&Mouse"",
                    ""action"": ""Pause"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""b1854652-b479-435b-88c2-bf39235ee1b7"",
                    ""path"": ""<Gamepad>/start"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": ""Gamepad"",
                    ""action"": ""Pause"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        },
        {
            ""name"": ""UI"",
            ""id"": ""2ec61166-6205-4f68-b61e-53b341eeb4bb"",
            ""actions"": [],
            ""bindings"": []
        }
    ],
    ""controlSchemes"": [
        {
            ""name"": ""Keyboard&Mouse"",
            ""bindingGroup"": ""Keyboard&Mouse"",
            ""devices"": [
                {
                    ""devicePath"": ""<Keyboard>"",
                    ""isOptional"": false,
                    ""isOR"": false
                },
                {
                    ""devicePath"": ""<Mouse>"",
                    ""isOptional"": true,
                    ""isOR"": false
                }
            ]
        },
        {
            ""name"": ""Gamepad"",
            ""bindingGroup"": ""Gamepad"",
            ""devices"": [
                {
                    ""devicePath"": ""<Gamepad>"",
                    ""isOptional"": false,
                    ""isOR"": false
                }
            ]
        }
    ]
}");
            m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
            m_Player_Movement = m_Player.FindAction("Movement", throwIfNotFound: true);
            m_Player_ActiveAbility = m_Player.FindAction("ActiveAbility", throwIfNotFound: true);
            m_Player_Pause = m_Player.FindAction("Pause", throwIfNotFound: true);
            m_UI = asset.FindActionMap("UI", throwIfNotFound: true);
        }

        ~@PlayerInputActions()
        {
            UnityEngine.Debug.Assert(!m_Player.enabled, "Player action map is still enabled.");
            UnityEngine.Debug.Assert(!m_UI.enabled, "UI action map is still enabled.");
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(asset);
        }

        public InputBinding? bindingMask
        {
            get => asset.bindingMask;
            set => asset.bindingMask = value;
        }

        public ReadOnlyArray<InputDevice>? devices
        {
            get => asset.devices;
            set => asset.devices = value;
        }

        public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

        public bool Contains(InputAction action) => asset.Contains(action);

        public IEnumerator<InputAction> GetEnumerator() => asset.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Enable() => asset.Enable();

        public void Disable() => asset.Disable();

        public IEnumerable<InputBinding> bindings => asset.bindings;

        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
            => asset.FindAction(actionNameOrId, throwIfNotFound);

        public int FindBinding(InputBinding bindingMask, out InputAction action)
            => asset.FindBinding(bindingMask, out action);

        // Player action map
        private readonly InputActionMap m_Player;
        private readonly InputAction m_Player_Movement;
        private readonly InputAction m_Player_ActiveAbility;
        private readonly InputAction m_Player_Pause;

        public struct PlayerActions
        {
            private @PlayerInputActions m_Wrapper;
            public PlayerActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }
            public InputAction @Movement => m_Wrapper.m_Player_Movement;
            public InputAction @ActiveAbility => m_Wrapper.m_Player_ActiveAbility;
            public InputAction @Pause => m_Wrapper.m_Player_Pause;
            public InputActionMap Get() => m_Wrapper.m_Player;
            public void Enable() => Get().Enable();
            public void Disable() => Get().Disable();
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(PlayerActions set) => set.Get();
        }

        public PlayerActions @Player => new PlayerActions(this);

        // UI action map
        private readonly InputActionMap m_UI;

        public struct UIActions
        {
            private @PlayerInputActions m_Wrapper;
            public UIActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }
            public InputActionMap Get() => m_Wrapper.m_UI;
            public void Enable() => Get().Enable();
            public void Disable() => Get().Disable();
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(UIActions set) => set.Get();
        }

        public UIActions @UI => new UIActions(this);
    }
}
