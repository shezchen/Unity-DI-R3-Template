using System;
using R3;

namespace Architecture.Data.GameSave
{
    public sealed record GameDataRuntime(DateTimeOffset LastSavedAtUtc)
    {
        public static GameDataRuntime NewGame() => new(DateTimeOffset.MinValue);
    }

    public enum GameSaveChangeType
    {
        NewGame,
        Saved,
        Loaded
    }

    public sealed record GameSaveChange(
        GameSaveChangeType Type,
        int SlotIndex,
        GameDataRuntime Game);

    public enum GameSaveOperationStatus
    {
        Success,
        Missing,
        NoActiveGame,
        InvalidData,
        Failed
    }

    public sealed record GameSaveOperationResult(
        GameSaveOperationStatus Status,
        string Error = null)
    {
        public bool IsSuccess => Status == GameSaveOperationStatus.Success;
    }

    public interface IGameSaveService
    {
        GameDataRuntime Current { get; }
        Observable<GameSaveChange> Changes { get; }

        GameSaveOperationResult NewGame(int slotIndex);
        GameSaveOperationResult Save(int slotIndex);
        GameSaveOperationResult Load(int slotIndex);
        bool SlotExists(int slotIndex);
    }
}
