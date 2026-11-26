namespace VubesClub.Api.Contracts;

/// <summary>Tam olarak biri dolu olmalı: yeni e-posta ya da yeni telefon (E.164).</summary>
public sealed record ContactChangeRequest(string? Email, string? Phone);
