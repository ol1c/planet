namespace Planet.Core.Model;

public readonly record struct Cell(bool HasPlant, bool HasAnimal, int AnimalAge = 0)
{
    public static Cell Empty => default;

    public int AnimalId { get; init; }

    public short Hunger { get; init; }

    public short Satiety { get; init; }

    public bool IsEmpty => !HasPlant && !HasAnimal;

    public Cell WithPlant(bool hasPlant) => this with { HasPlant = hasPlant };

    public Cell WithAnimal(int age, int id = 0) =>
        this with { HasAnimal = true, AnimalAge = age, AnimalId = id, Hunger = 0, Satiety = 0 };

    public Cell WithoutAnimal() =>
        this with { HasAnimal = false, AnimalAge = 0, AnimalId = 0, Hunger = 0, Satiety = 0 };
}
