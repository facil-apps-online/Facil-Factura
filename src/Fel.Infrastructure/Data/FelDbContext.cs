using Fel.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fel.Infrastructure.Data
{
    public class FelDbContext : DbContext
    {
        public FelDbContext(DbContextOptions<FelDbContext> options) : base(options) { }

        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Resolution> Resolutions => Set<Resolution>();
        public DbSet<Certificate> Certificates => Set<Certificate>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
        public DbSet<TenantPricing> TenantPricings => Set<TenantPricing>();
        public DbSet<TenantUser> TenantUsers { get; set; }
        public DbSet<ClientUser> ClientUsers { get; set; }
        public DbSet<TenantBilling> TenantBillings => Set<TenantBilling>();
        public DbSet<SuperadminUser> SuperadminUsers => Set<SuperadminUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Email).HasMaxLength(150);
                
                entity.Property(e => e.Slug).HasMaxLength(100);
                entity.HasIndex(e => e.Slug).IsUnique();
            });

            modelBuilder.Entity<Client>(entity =>
            {
                entity.ToTable("Clients");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(150);
                entity.Property(e => e.TaxId).IsRequired().HasMaxLength(50);
                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.Clients)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.LiveApiKey).HasMaxLength(100);
                entity.HasIndex(e => e.LiveApiKey).IsUnique();
                
                entity.Property(e => e.TestApiKey).HasMaxLength(100);
                entity.HasIndex(e => e.TestApiKey).IsUnique();
            });

            modelBuilder.Entity<Resolution>(entity =>
            {
                entity.ToTable("Resolutions");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client)
                      .WithMany(c => c.Resolutions)
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Certificate>(entity =>
            {
                entity.ToTable("Certificates");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client)
                      .WithMany(c => c.Certificates)
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("Documents");
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.Client)
                      .WithMany(c => c.Documents)
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.DocumentType)
                      .WithMany(d => d.Documents)
                      .HasForeignKey(e => e.DocumentTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
                
                entity.Property(e => e.TrackingId).HasMaxLength(100);
                entity.Property(e => e.Number).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.PriceCharged).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<DocumentType>(entity =>
            {
                entity.ToTable("DocumentTypes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.DianCode).IsRequired().HasMaxLength(10);
                entity.Property(e => e.OperationType).HasMaxLength(10);
                entity.Property(e => e.CustomizationId).HasMaxLength(150);

                entity.HasData(
                    // Facturas de Venta
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Code = "FE-STD", Name = "Factura de Venta - Estándar", Description = "Factura Electrónica de Venta", DianCode = "01", OperationType = "10" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Code = "FE-SALUD", Name = "Factura de Venta - Sector Salud", Description = "Factura Electrónica con RIPS", DianCode = "01", OperationType = "10" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Code = "FE-AIU", Name = "Factura de Venta - AIU", Description = "Servicios AIU", DianCode = "01", OperationType = "09" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Code = "FE-MANDATO", Name = "Factura de Venta - Mandatos", Description = "Factura bajo Mandato", DianCode = "01", OperationType = "11" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Code = "FE-TRANSP", Name = "Factura de Venta - Transporte", Description = "Servicio de Transporte de Carga", DianCode = "01", OperationType = "15" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000006"), Code = "FE-EXP", Name = "Factura de Venta - Exportación", Description = "Factura de Exportación", DianCode = "02", OperationType = "10" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000007"), Code = "FC-FACT", Name = "Factura de Contingencia Facturador", Description = "Contingencia del obligado a facturar", DianCode = "03", OperationType = "10" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000008"), Code = "FC-DIAN", Name = "Factura de Contingencia DIAN", Description = "Contingencia tipo DIAN", DianCode = "04", OperationType = "10" },
                    
                    // Notas
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000009"), Code = "NC", Name = "Nota Crédito", Description = "Nota Crédito Electrónica", DianCode = "91", OperationType = "20" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000010"), Code = "ND", Name = "Nota Débito", Description = "Nota Débito Electrónica", DianCode = "92", OperationType = "30" },
                    
                    // Documentos Equivalentes
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000011"), Code = "DE-POS", Name = "Doc. Equivalente - Tiquete POS", Description = "Tiquete de máquina registradora POS", DianCode = "20" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000012"), Code = "DE-CINE", Name = "Doc. Equivalente - Cine", Description = "Boleta de ingreso a cine", DianCode = "06" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000013"), Code = "DE-PASAJEROS", Name = "Doc. Equivalente - Transporte Pasajeros", Description = "Tiquete de transporte de pasajeros", DianCode = "07" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000014"), Code = "DE-EXTRACTO", Name = "Doc. Equivalente - Extracto", Description = "Extracto expedido por sociedades", DianCode = "08" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000015"), Code = "DE-AEREO", Name = "Doc. Equivalente - Transporte Aéreo", Description = "Tiquete de transporte aéreo", DianCode = "09" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000016"), Code = "DE-JUEGOSLOC", Name = "Doc. Equivalente - Juegos Localizados", Description = "Documento en juegos localizados", DianCode = "10" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000017"), Code = "DE-AZAR", Name = "Doc. Equivalente - Suerte y Azar", Description = "Boletas en juegos de suerte y azar", DianCode = "11" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000018"), Code = "DE-PEAJE", Name = "Doc. Equivalente - Peajes", Description = "Cobro de peajes", DianCode = "12" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000019"), Code = "DE-BOLSA", Name = "Doc. Equivalente - Bolsa de Valores", Description = "Operaciones Bolsa de Valores", DianCode = "13" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000020"), Code = "DE-AGRO", Name = "Doc. Equivalente - Bolsa Agropecuaria", Description = "Operaciones Bolsa Agropecuaria", DianCode = "14" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000021"), Code = "DE-SERVICIOSP", Name = "Doc. Equivalente - Servicios Públicos", Description = "Servicios públicos domiciliarios", DianCode = "15" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000022"), Code = "DE-ESPECTACULOS", Name = "Doc. Equivalente - Espectáculos Públicos", Description = "Ingreso a espectáculos públicos", DianCode = "16" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000023"), Code = "DE-AJUSTE", Name = "Nota de Ajuste - Doc. Equivalente", Description = "Nota de ajuste para documentos equivalentes", DianCode = "94" },
                    
                    // Documento Soporte
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000024"), Code = "DS", Name = "Doc. Soporte - Adquisiciones a No Obligados", Description = "Documento soporte", DianCode = "05" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000025"), Code = "DS-AJUSTE", Name = "Nota de Ajuste - Doc. Soporte", Description = "Ajuste a documento soporte", DianCode = "95" },
                    
                    // Nómina
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000026"), Code = "NE-PAGO", Name = "Nómina Electrónica", Description = "Pago de nómina electrónica", DianCode = "102" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000027"), Code = "NE-AJUSTE", Name = "Nota de Ajuste - Nómina Electrónica", Description = "Ajuste de nómina electrónica", DianCode = "103" }
                );
            });

            modelBuilder.Entity<TenantPricing>(entity =>
            {
                entity.ToTable("TenantPricings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PricePerDocument).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).HasMaxLength(10);
                
                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.Pricings)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(e => e.DocumentType)
                      .WithMany(d => d.Pricings)
                      .HasForeignKey(e => e.DocumentTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TenantBilling>(entity =>
            {
                entity.ToTable("TenantBillings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).HasMaxLength(10);
                entity.Property(e => e.Status).HasMaxLength(50);
                
                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.Billings)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SuperadminUser>(entity =>
            {
                entity.ToTable("SuperadminUsers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
            });
        }
    }
}
