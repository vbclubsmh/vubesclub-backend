namespace VubesClub.Api.Contracts;

/// <summary>Boş bırakılan alan değişmez. Doğum tarihi yalnızca bir kez girilebilir.</summary>
public sealed record UpdateProfile(int? HeightCm, int? WeightKg, string? ClothingSize, DateOnly? BirthDate);
