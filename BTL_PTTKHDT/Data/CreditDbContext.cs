using Microsoft.EntityFrameworkCore;

namespace BTL_PTTKHDT.Data;

public sealed class CreditDbContext : DbContext
{
    public CreditDbContext(DbContextOptions<CreditDbContext> options) : base(options)
    {
    }

    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoanApplication>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Code).HasMaxLength(32);
            entity.Property(x => x.CustomerName).HasMaxLength(256);
            entity.Property(x => x.Status).HasMaxLength(32);
        });

        var seedYear = 2023;
        modelBuilder.Entity<LoanApplication>().HasData(
            new LoanApplication
            {
                Id = 1,
                Code = "HD-2023-0142",
                CustomerName = "Nguyễn Văn A",
                Amount = 1_200_000_000m,
                Status = "Chờ duyệt",
                CreatedAt = new DateTime(seedYear, 6, 10, 9, 0, 0, DateTimeKind.Utc),
                DisbursedAt = null,
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 2,
                Code = "HD-2023-0141",
                CustomerName = "Công ty TNHH Hưng Thịnh",
                Amount = 5_500_000_000m,
                Status = "Đang soạn",
                CreatedAt = new DateTime(seedYear, 6, 9, 14, 30, 0, DateTimeKind.Utc),
                DisbursedAt = null,
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 3,
                Code = "HD-2023-0140",
                CustomerName = "Trần Thị B",
                Amount = 850_000_000m,
                Status = "Chờ duyệt",
                CreatedAt = new DateTime(seedYear, 6, 8, 10, 15, 0, DateTimeKind.Utc),
                DisbursedAt = null,
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 4,
                Code = "HD-2023-0139",
                CustomerName = "Lê Văn C",
                Amount = 2_100_000_000m,
                Status = "Đang soạn",
                CreatedAt = new DateTime(seedYear, 6, 7, 16, 0, 0, DateTimeKind.Utc),
                DisbursedAt = null,
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 5,
                Code = "HD-2023-0138",
                CustomerName = "Nguyễn Văn A",
                Amount = 1_600_000_000m,
                Status = "Đã giải ngân",
                CreatedAt = new DateTime(seedYear, 5, 18, 9, 0, 0, DateTimeKind.Utc),
                DisbursedAt = new DateTime(seedYear, 5, 22, 9, 0, 0, DateTimeKind.Utc),
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 6,
                Code = "HD-2023-0137",
                CustomerName = "Công ty Cổ phần Minh Long",
                Amount = 3_200_000_000m,
                Status = "Đã giải ngân",
                CreatedAt = new DateTime(seedYear, 4, 2, 9, 0, 0, DateTimeKind.Utc),
                DisbursedAt = new DateTime(seedYear, 4, 10, 9, 0, 0, DateTimeKind.Utc),
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 7,
                Code = "HD-2023-0136",
                CustomerName = "Phạm Thị D",
                Amount = 900_000_000m,
                Status = "Nợ xấu",
                CreatedAt = new DateTime(seedYear, 3, 12, 9, 0, 0, DateTimeKind.Utc),
                DisbursedAt = new DateTime(seedYear, 3, 20, 9, 0, 0, DateTimeKind.Utc),
                IsNonPerforming = true
            },
            new LoanApplication
            {
                Id = 8,
                Code = "HD-2023-0135",
                CustomerName = "Ngô Văn E",
                Amount = 1_050_000_000m,
                Status = "Đã giải ngân",
                CreatedAt = new DateTime(seedYear, 2, 1, 9, 0, 0, DateTimeKind.Utc),
                DisbursedAt = new DateTime(seedYear, 2, 15, 9, 0, 0, DateTimeKind.Utc),
                IsNonPerforming = false
            },
            new LoanApplication
            {
                Id = 9,
                Code = "HD-2023-0134",
                CustomerName = "Công ty TNHH An Phát",
                Amount = 2_450_000_000m,
                Status = "Đã giải ngân",
                CreatedAt = new DateTime(seedYear, 1, 5, 9, 0, 0, DateTimeKind.Utc),
                DisbursedAt = new DateTime(seedYear, 1, 25, 9, 0, 0, DateTimeKind.Utc),
                IsNonPerforming = false
            }
        );

        base.OnModelCreating(modelBuilder);
    }
}
