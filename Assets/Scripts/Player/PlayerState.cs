namespace Player
{
    public enum PlayerState
    {
        // Basic Movement
        Idle,
        Moving,
        Falling,
        
        CUSTOM_MOVEMENT, // This is only used as a marker...
        
        // Sprint Actions
        Slide,
        WallRun,
        RailGrind,
        
        // Abilities
        Ability1,
        Ability2,
        Ability3
    }
}