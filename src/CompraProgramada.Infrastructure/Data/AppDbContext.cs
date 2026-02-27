using CompraProgramada.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ContaGrafica> ContasGraficas => Set<ContaGrafica>();
    public DbSet<CustodiaFilhote> CustodiasFilhote => Set<CustodiaFilhote>();
    public DbSet<CustodiaMaster> CustodiasMaster => Set<CustodiaMaster>();
    public DbSet<CestaRecomendacao> CestasRecomendacao => Set<CestaRecomendacao>();
    public DbSet<CestaItem> CestaItens => Set<CestaItem>();
    public DbSet<OrdemCompra> OrdensCompra => Set<OrdemCompra>();
    public DbSet<Distribuicao> Distribuicoes => Set<Distribuicao>();
    public DbSet<ExecucaoCompra> ExecucoesCompra => Set<ExecucaoCompra>();
    public DbSet<HistoricoAporte> HistoricoAportes => Set<HistoricoAporte>();
    public DbSet<HistoricoValorMensal> HistoricoValoresMensais => Set<HistoricoValorMensal>();
    public DbSet<VendaRebalanceamento> VendasRebalanceamento => Set<VendaRebalanceamento>();
    public DbSet<CotacaoB3> Cotacoes => Set<CotacaoB3>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Cpf).IsUnique();
            entity.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cpf).HasMaxLength(11).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ValorMensal).HasPrecision(18, 2);
            entity.HasOne(e => e.ContaGrafica)
                  .WithOne(c => c.Cliente)
                  .HasForeignKey<ContaGrafica>(c => c.ClienteId);
            entity.HasMany(e => e.CustodiaFilhote)
                  .WithOne(c => c.Cliente)
                  .HasForeignKey(c => c.ClienteId);
            entity.HasMany(e => e.HistoricoAportes)
                  .WithOne(h => h.Cliente)
                  .HasForeignKey(h => h.ClienteId);
            entity.HasMany(e => e.HistoricoValoresMensais)
                  .WithOne(h => h.Cliente)
                  .HasForeignKey(h => h.ClienteId);
        });

        modelBuilder.Entity<ContaGrafica>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroConta).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<CustodiaFilhote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ClienteId, e.Ticker }).IsUnique();
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoMedio).HasPrecision(18, 4);
            entity.Property(e => e.ValorTotalInvestido).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CustodiaMaster>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Ticker).IsUnique();
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoMedio).HasPrecision(18, 4);
            entity.Property(e => e.Origem).HasMaxLength(200);
        });

        modelBuilder.Entity<CestaRecomendacao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nome).HasMaxLength(200).IsRequired();
            entity.HasMany(e => e.Itens)
                  .WithOne(i => i.CestaRecomendacao)
                  .HasForeignKey(i => i.CestaRecomendacaoId);
        });

        modelBuilder.Entity<CestaItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.Percentual).HasPrecision(5, 2);
        });

        modelBuilder.Entity<OrdemCompra>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoUnitario).HasPrecision(18, 2);
            entity.Property(e => e.ValorTotal).HasPrecision(18, 2);
            entity.Property(e => e.TipoOperacao).HasConversion<string>().HasMaxLength(10);
        });

        modelBuilder.Entity<Distribuicao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoUnitario).HasPrecision(18, 2);
            entity.Property(e => e.ValorOperacao).HasPrecision(18, 2);
            entity.Property(e => e.ValorIRDedoDuro).HasPrecision(18, 4);
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.ClienteId);
        });

        modelBuilder.Entity<ExecucaoCompra>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DataReferencia).IsUnique();
            entity.Property(e => e.TotalConsolidado).HasPrecision(18, 2);
            entity.Property(e => e.Parcela).HasMaxLength(5);
        });

        modelBuilder.Entity<HistoricoAporte>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Valor).HasPrecision(18, 2);
            entity.Property(e => e.Parcela).HasMaxLength(5);
        });

        modelBuilder.Entity<HistoricoValorMensal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValorAnterior).HasPrecision(18, 2);
            entity.Property(e => e.ValorNovo).HasPrecision(18, 2);
        });

        modelBuilder.Entity<VendaRebalanceamento>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.PrecoVenda).HasPrecision(18, 2);
            entity.Property(e => e.PrecoMedio).HasPrecision(18, 4);
            entity.Property(e => e.ValorVenda).HasPrecision(18, 2);
            entity.Property(e => e.Lucro).HasPrecision(18, 2);
            entity.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.ClienteId);
        });

        modelBuilder.Entity<CotacaoB3>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Ticker, e.DataPregao });
            entity.Property(e => e.Ticker).HasMaxLength(12).IsRequired();
            entity.Property(e => e.CodigoBDI).HasMaxLength(2);
            entity.Property(e => e.NomeEmpresa).HasMaxLength(50);
            entity.Property(e => e.PrecoAbertura).HasPrecision(18, 2);
            entity.Property(e => e.PrecoMaximo).HasPrecision(18, 2);
            entity.Property(e => e.PrecoMinimo).HasPrecision(18, 2);
            entity.Property(e => e.PrecoFechamento).HasPrecision(18, 2);
            entity.Property(e => e.PrecoMedio).HasPrecision(18, 2);
            entity.Property(e => e.VolumeNegociado).HasPrecision(18, 2);
        });
    }
}
