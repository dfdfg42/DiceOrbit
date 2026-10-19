using System.Collections;
using DiceOrbit.Core;
using DiceOrbit.Core.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiceOrbit.Visuals
{
    /// <summary>Standalone art-review scene. Starts a sample encounter without starting/saving a run.</summary>
    public sealed class WitchTowerPreviewEntry : MonoBehaviour
    {
        public CharacterPreset[] party;
        public EncounterDefinition encounter;
        private Mouse captureMouse;

        private void Awake()
        {
            // Batch capture has no native mouse; supply a test device for the existing hover UI.
            if (Application.isBatchMode && Mouse.current == null)
                captureMouse = InputSystem.AddDevice<Mouse>();
        }

        private IEnumerator Start()
        {
            // Let the original scene managers, orbit and UI initialize first.
            yield return null;
            yield return null;
            var spawner = FindFirstObjectByType<CharacterSpawner>();
            var spawned = new Character[party.Length];
            for (int i = 0; i < party.Length; i++) spawned[i] = spawner.Spawn(party[i], i, party.Length);
            // Character.InitializeAfterDelay must finish BEFORE the intro raises tile transforms.
            // Otherwise the bootstrap places characters on the temporarily airborne tiles.
            foreach (var character in spawned)
                while (character != null && character.CurrentTile == null) yield return null;
            var flow = GameFlowManager.Instance;
            flow.ChangeState(GameState.Combat);
            // Keep the initialized combat UI, but detach run/result routing in this isolated scene.
            // In particular, preview defeat must never reach GameOver's RunSaveService.Delete().
            Destroy(flow);
            yield return null;
            CombatManager.Instance.StartEncounter(encounter, 1);
        }

        private void OnDestroy()
        {
            if (captureMouse != null && captureMouse.added) InputSystem.RemoveDevice(captureMouse);
        }
    }
}
