using Pattern.Core.Model;

namespace Pattern.PublicServices.Interfaces;

public interface ISizeChartService
{
    IReadOnlyList<SizeRow> GetAll();
    string                 ExportCsv();
}