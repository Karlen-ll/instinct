namespace Instinct.Core.State
{
    // Индексы битов (0..63) для сдвига: 1UL << (int)flag
    public enum WorldFlag : byte
    {
        HasWeapon = 0,
        HasAmmo = 1,
        IsTargetVisible = 2,
        IsInCover = 3,
        IsTargetDead = 4,
        IsUnderFire = 5,
        SquadNeedsSupport = 6
    }
}