namespace PatternPro.Core.Persistence;

/// <summary>
/// Marker for the PatternPro data access layer (implemented in <c>PatternPro.DataAccess</c>).
/// Register via <c>AddPatternProDataAccess</c> and inject repository interfaces from
/// <c>PatternPro.Core.Persistence.Repositories</c> in application services.
/// </summary>
public interface IDataAccessLayer;
