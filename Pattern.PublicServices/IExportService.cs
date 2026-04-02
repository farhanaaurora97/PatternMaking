namespace Pattern.PublicServices.Interfaces;

public interface IExportService
{
    IReadOnlyList<string> GetExportSteps(string format);
}