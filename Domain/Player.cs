using testapi1.Domain;

public class Player
{
    public int PlayerId { get; set; }
    public string DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<PlayerNpcState> NpcStates { get; set; }
    public ICollection<Interaction> Interactions { get; set; }
}