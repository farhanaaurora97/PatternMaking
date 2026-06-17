using Microsoft.EntityFrameworkCore;
using PatternEntity = Pattern.Core.Model.Pattern;

namespace PatternPro.DataAccess.Persistence;

public class PatternProDbContext : DbContext
{
    /// <summary>PostgreSQL schema for all PatternPro tables (same idea as ERP modules: Accounting, Inventory, …).</summary>
    public const string SchemaName = "patternpro";

    public DbSet<PatternEntity> Patterns => Set<PatternEntity>();
    public DbSet<AppKeyValue> AppKeyValues => Set<AppKeyValue>();
    public DbSet<PieceEntity> Pieces => Set<PieceEntity>();
    public DbSet<PieceVertexEntity> PieceVertices => Set<PieceVertexEntity>();

    public DbSet<SizeChartColumnEntity> SizeChartColumns => Set<SizeChartColumnEntity>();
    public DbSet<SizeChartRowEntity> SizeChartRows => Set<SizeChartRowEntity>();
    public DbSet<SizeChartValueEntity> SizeChartValues => Set<SizeChartValueEntity>();

    public DbSet<GradingMetaEntity> GradingMeta => Set<GradingMetaEntity>();
    public DbSet<GradingColumnEntity> GradingColumns => Set<GradingColumnEntity>();
    public DbSet<GradingStyleEntity> GradingStyles => Set<GradingStyleEntity>();
    public DbSet<GradingRowEntity> GradingRows => Set<GradingRowEntity>();
    public DbSet<GradingDeltaEntity> GradingDeltas => Set<GradingDeltaEntity>();

    public DbSet<MeasurementProfileEntity> MeasurementProfiles => Set<MeasurementProfileEntity>();
    public DbSet<MeasurementProfileValueEntity> MeasurementProfileValues => Set<MeasurementProfileValueEntity>();

    public DbSet<EaseOverrideEntity> EaseOverrides => Set<EaseOverrideEntity>();
    public DbSet<AppUserEntity> AppUsers => Set<AppUserEntity>();

    public PatternProDbContext(DbContextOptions<PatternProDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PatternEntity>(entity =>
        {
            entity.ToTable("patterns", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Code).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Style).HasMaxLength(128);
            entity.Property(e => e.BaseSize).HasMaxLength(32);
            entity.Property(e => e.Status).HasMaxLength(64);
            entity.Property(e => e.Date).HasMaxLength(32);
            entity.Property(e => e.Designer).HasMaxLength(256);
            entity.Property(e => e.Revision).HasMaxLength(32);
            entity.Property(e => e.FabricStretchPercent).HasPrecision(5, 2);
            entity.Property(e => e.Season).HasMaxLength(32);
            entity.Property(e => e.Owner).HasMaxLength(256);
            entity.Property(e => e.LifecycleStatus).HasMaxLength(32);
            entity.Property(e => e.Category).HasMaxLength(128);
            entity.Property(e => e.ApprovedBy).HasMaxLength(256);
            entity.Property(e => e.CutterTestedBy).HasMaxLength(256);
            entity.Property(e => e.CutterTestNotes).HasMaxLength(2000);
            entity.Property(e => e.CloReviewNotes).HasMaxLength(2000);
            entity.Property(e => e.ShrinkagePercent).HasPrecision(5, 2);
        });

        modelBuilder.Entity<AppKeyValue>(entity =>
        {
            entity.ToTable("app_kv", SchemaName);
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
        });

        modelBuilder.Entity<PieceEntity>(entity =>
        {
            entity.ToTable("pieces", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StyleKey).HasMaxLength(64);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.PieceNumber).HasMaxLength(16);
            entity.Property(e => e.Material).HasMaxLength(128);
            entity.Property(e => e.Cut).HasMaxLength(64);
            entity.Property(e => e.Color).HasMaxLength(32);
            entity.Property(e => e.Category).HasMaxLength(128);
            entity.Property(e => e.GrainLine).HasMaxLength(64);
            entity.Property(e => e.SeamAllowanceJoin).HasMaxLength(16);
            entity.HasIndex(e => e.PatternId);
            entity.HasIndex(e => e.StyleKey);
        });

        modelBuilder.Entity<PieceVertexEntity>(entity =>
        {
            entity.ToTable("piece_vertices", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).HasMaxLength(16);
            entity.HasIndex(e => new { e.PieceId, e.Kind, e.PointOrder });
            entity.HasOne(e => e.Piece)
                .WithMany(e => e.Vertices)
                .HasForeignKey(e => e.PieceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SizeChartColumnEntity>(entity =>
        {
            entity.ToTable("size_chart_columns", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(32);
            entity.HasIndex(e => e.SortOrder).IsUnique();
        });

        modelBuilder.Entity<SizeChartRowEntity>(entity =>
        {
            entity.ToTable("size_chart_rows", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MeasurementPoint).HasMaxLength(128);
            entity.Property(e => e.ToleranceCm).HasPrecision(6, 2);
            entity.Property(e => e.MeasurementMethod).HasMaxLength(256);
            entity.HasIndex(e => e.SortOrder).IsUnique();
        });

        modelBuilder.Entity<SizeChartValueEntity>(entity =>
        {
            entity.ToTable("size_chart_values", SchemaName);
            entity.HasKey(e => new { e.RowId, e.ColumnIndex });
            entity.HasOne(e => e.Row)
                .WithMany(e => e.Values)
                .HasForeignKey(e => e.RowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GradingMetaEntity>(entity =>
        {
            entity.ToTable("grading_meta", SchemaName);
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<GradingColumnEntity>(entity =>
        {
            entity.ToTable("grading_columns", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasMaxLength(32);
            entity.HasIndex(e => e.SortOrder).IsUnique();
        });

        modelBuilder.Entity<GradingStyleEntity>(entity =>
        {
            entity.ToTable("grading_styles", SchemaName);
            entity.HasKey(e => e.StyleKey);
            entity.Property(e => e.StyleKey).HasMaxLength(64);
            entity.Property(e => e.Label).HasMaxLength(128);
        });

        modelBuilder.Entity<GradingRowEntity>(entity =>
        {
            entity.ToTable("grading_rows", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StyleKey).HasMaxLength(64);
            entity.Property(e => e.MeasurementPoint).HasMaxLength(128);
            entity.HasIndex(e => new { e.StyleKey, e.MeasurementPoint }).IsUnique();
            entity.HasOne(e => e.Style)
                .WithMany(e => e.Rows)
                .HasForeignKey(e => e.StyleKey)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GradingDeltaEntity>(entity =>
        {
            entity.ToTable("grading_deltas", SchemaName);
            entity.HasKey(e => new { e.RowId, e.ColumnIndex });
            entity.HasOne(e => e.Row)
                .WithMany(e => e.Deltas)
                .HasForeignKey(e => e.RowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MeasurementProfileEntity>(entity =>
        {
            entity.ToTable("measurement_profiles", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<MeasurementProfileValueEntity>(entity =>
        {
            entity.ToTable("measurement_profile_values", SchemaName);
            entity.HasKey(e => new { e.ProfileId, e.MeasurementPoint });
            entity.Property(e => e.MeasurementPoint).HasMaxLength(128);
            entity.HasOne(e => e.Profile)
                .WithMany(e => e.Values)
                .HasForeignKey(e => e.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EaseOverrideEntity>(entity =>
        {
            entity.ToTable("ease_overrides", SchemaName);
            entity.HasKey(e => new { e.StyleKey, e.MeasurementPoint });
            entity.Property(e => e.StyleKey).HasMaxLength(64);
            entity.Property(e => e.MeasurementPoint).HasMaxLength(128);
        });

        modelBuilder.Entity<AppUserEntity>(entity =>
        {
            entity.ToTable("app_users", SchemaName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeId).HasMaxLength(32);
            entity.Property(e => e.UserName).HasMaxLength(64);
            entity.Property(e => e.DisplayName).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(32);
            entity.Property(e => e.PasswordHash).HasMaxLength(512);
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.HasIndex(e => e.EmployeeId).IsUnique();
        });
    }
}
