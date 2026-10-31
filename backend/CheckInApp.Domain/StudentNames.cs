namespace CheckInApp.Domain;

public static class StudentNames
{
    public static string Clean(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    public static string Full(string familyName, string givenName) => Clean(familyName + " " + givenName);
    public static (string FamilyName, string GivenName) Split(string fullName)
    {
        var clean = Clean(fullName);
        var last = clean.LastIndexOf(' ');
        return last < 0 ? ("", clean) : (clean[..last], clean[(last + 1)..]);
    }
}
