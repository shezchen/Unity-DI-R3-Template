using System;
using Architecture.Data.Persistence;
using R3;
using UnityEngine;

namespace Architecture.Data.GameSave
{
    public sealed class GameSaveService : IGameSaveService, IDisposable
    {
        private readonly IGameSaveRepository _repository;
        private readonly IClock _clock;
        private readonly Subject<GameSaveChange> _changes = new();

        public GameSaveService(IGameSaveRepository repository, IClock clock)
        {
            _repository = repository;
            _clock = clock;
        }

        public GameDataRuntime Current { get; private set; }
        public Observable<GameSaveChange> Changes => _changes;

        public GameSaveOperationResult NewGame(int slotIndex)
        {
            var candidate = new GameDataRuntime(LastSavedAtUtc: _clock.UtcNow);
            var result = _repository.Save(slotIndex, candidate);
            if (!result.IsSuccess)
            {
                LogFailure("create new game", slotIndex, result);
                return result;
            }

            Current = candidate;
            _changes.OnNext(new GameSaveChange(GameSaveChangeType.NewGame, slotIndex, Current));
            return result;
        }

        public GameSaveOperationResult Save(int slotIndex)
        {
            if (Current == null)
            {
                return new GameSaveOperationResult(
                    GameSaveOperationStatus.NoActiveGame,
                    "Cannot save before creating or loading a game.");
            }

            var candidate = new GameDataRuntime(LastSavedAtUtc: _clock.UtcNow);
            var result = _repository.Save(slotIndex, candidate);
            if (!result.IsSuccess)
            {
                LogFailure("save game", slotIndex, result);
                return result;
            }

            Current = candidate;
            _changes.OnNext(new GameSaveChange(GameSaveChangeType.Saved, slotIndex, Current));
            return result;
        }

        public GameSaveOperationResult Load(int slotIndex)
        {
            var load = _repository.Load(slotIndex);
            if (load.Status != GameSaveOperationStatus.Success)
            {
                LogFailure("load game", slotIndex, new GameSaveOperationResult(load.Status, load.Error));
                return new GameSaveOperationResult(load.Status, load.Error);
            }

            Current = load.Game;
            _changes.OnNext(new GameSaveChange(GameSaveChangeType.Loaded, slotIndex, Current));

            if (load.RecoveredFromBackup)
            {
                var restore = _repository.Save(slotIndex, Current);
                if (!restore.IsSuccess)
                {
                    Debug.LogWarning($"[GameSave] Slot {slotIndex} backup loaded but primary restore failed: {restore.Error}");
                }
            }

            return new GameSaveOperationResult(GameSaveOperationStatus.Success);
        }

        public bool SlotExists(int slotIndex) => _repository.Exists(slotIndex);

        public void Dispose() => _changes.Dispose();

        private static void LogFailure(string operation, int slotIndex, GameSaveOperationResult result)
        {
            var message = $"[GameSave] Failed to {operation} in slot {slotIndex}: {result.Status}. {result.Error}";
            if (result.Status == GameSaveOperationStatus.Missing)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }
    }
}
