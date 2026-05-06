namespace Pattern.PublicServices.Interfaces;

public interface IExportService
{
    IReadOnlyList<string> GetExportSteps(string format);
    (byte[] Bytes, string ContentType, string FileName) BuildExportPackage(
        string style,
        string format,
        IReadOnlyList<string> sizes,
        int patternId = 0);
}