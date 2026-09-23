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
        public DbSet<ReceivedDocument> ReceivedDocuments => Set<ReceivedDocument>();
        public DbSet<ReceivedDocumentEvent> ReceivedDocumentEvents => Set<ReceivedDocumentEvent>();
        public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
        public DbSet<TenantPricing> TenantPricings => Set<TenantPricing>();
        public DbSet<TenantUser> TenantUsers { get; set; }
        public DbSet<TenantUserAssignment> TenantUserAssignments => Set<TenantUserAssignment>();
        public DbSet<ClientUser> ClientUsers { get; set; }
        public DbSet<TenantBilling> TenantBillings => Set<TenantBilling>();
        public DbSet<SuperadminUser> SuperadminUsers => Set<SuperadminUser>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
        public DbSet<ClientDocumentSetting> ClientDocumentSettings => Set<ClientDocumentSetting>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductTax> ProductTaxes => Set<ProductTax>();
        public DbSet<DocumentItem> DocumentItems { get; set; }
        public DbSet<DocumentRetention> DocumentRetentions => Set<DocumentRetention>();
        public DbSet<DocumentGeneralRetention> DocumentGeneralRetentions => Set<DocumentGeneralRetention>();
        public DbSet<TenantUserPricing> TenantUserPricings => Set<TenantUserPricing>();
        public DbSet<PrepaidPackage> PrepaidPackages => Set<PrepaidPackage>();
        public DbSet<TenantPrepaidBag> TenantPrepaidBags => Set<TenantPrepaidBag>();
        public DbSet<DataicoTaxCatalogItem> DataicoTaxCatalogItems => Set<DataicoTaxCatalogItem>();
        public DbSet<RetentionConcept> RetentionConcepts => Set<RetentionConcept>();
        public DbSet<TaxParameter> TaxParameters => Set<TaxParameter>();
        public DbSet<Associate> Associates => Set<Associate>();
        public DbSet<DianMunicipality> DianMunicipalities => Set<DianMunicipality>();
        public DbSet<Integrator> Integrators => Set<Integrator>();
        public DbSet<ClientIntegratorAssignment> ClientIntegratorAssignments => Set<ClientIntegratorAssignment>();
        public DbSet<TenantIntegratorBilling> TenantIntegratorBillings => Set<TenantIntegratorBilling>();
        public DbSet<ClientIntegratorBilling> ClientIntegratorBillings => Set<ClientIntegratorBilling>();
        public DbSet<ClientPrepaidPackage> ClientPrepaidPackages => Set<ClientPrepaidPackage>();
        public DbSet<ClientPrepaidBag> ClientPrepaidBags => Set<ClientPrepaidBag>();
        public DbSet<ClientEnabledDocumentType> ClientEnabledDocumentTypes => Set<ClientEnabledDocumentType>();
        public DbSet<ClientEnabledRetentionConcept> ClientEnabledRetentionConcepts => Set<ClientEnabledRetentionConcept>();
        public DbSet<DeveloperUser> DeveloperUsers => Set<DeveloperUser>();

        // RIPS Clinical Validation Rules
        public DbSet<RipsCupsRule> RipsCupsRules { get; set; }
        public DbSet<RipsCie10Rule> RipsCie10Rules { get; set; }

        public DbSet<TariffTier> TariffTiers { get; set; }

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
                entity.Property(e => e.CoreTenantId).HasMaxLength(64);

                entity.Property(e => e.LegalName).HasMaxLength(200);
                entity.Property(e => e.ContactPerson).HasMaxLength(150);
                entity.Property(e => e.ContactEmail).HasMaxLength(150);
                entity.Property(e => e.ContactPhone).HasMaxLength(30);
                entity.Property(e => e.WhatsAppPhone).HasMaxLength(30);
                entity.Property(e => e.EinvoicingEmail).HasMaxLength(150);
                entity.Property(e => e.CommercialEmail).HasMaxLength(150);
                entity.Property(e => e.Website).HasMaxLength(200);
                entity.Property(e => e.PhysicalAddressLine1).HasMaxLength(200);
                entity.Property(e => e.PhysicalAddressLine2).HasMaxLength(200);
                entity.Property(e => e.PhysicalCity).HasMaxLength(100);
                entity.Property(e => e.PhysicalState).HasMaxLength(100);
                entity.Property(e => e.PhysicalPostalCode).HasMaxLength(20);
                entity.Property(e => e.BillingAddress).HasMaxLength(300);
                entity.Property(e => e.DefaultLanguageCode).HasMaxLength(10);
                entity.Property(e => e.DefaultTimezone).HasMaxLength(50);
                entity.Property(e => e.DefaultCurrencyId).HasMaxLength(64);

                entity.HasOne(e => e.ParentTenant)
                      .WithMany(t => t.ChildTenants)
                      .HasForeignKey(e => e.ParentTenantId)
                      .OnDelete(DeleteBehavior.Restrict);
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

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);
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
                
                entity.HasOne(e => e.UsedTemplate)
                      .WithMany()
                      .HasForeignKey(e => e.UsedTemplateId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Customer)
                      .WithMany()
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ReferenceDocument)
                      .WithMany()
                      .HasForeignKey(e => e.ReferenceDocumentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Resolution)
                      .WithMany()
                      .HasForeignKey(e => e.ResolutionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.Subtotal).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");
                entity.Property(e => e.GeneralDiscountAmount).HasColumnType("decimal(18,4)");
            });

            modelBuilder.Entity<DocumentItem>(entity =>
            {
                entity.ToTable("DocumentItems");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(250);
                
                entity.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TaxRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.DiscountRate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,4)");

                entity.HasOne(e => e.Document)
                      .WithMany(d => d.Items)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<DocumentRetention>(entity =>
            {
                entity.ToTable("DocumentRetentions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TaxCategory).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Rate).HasColumnType("decimal(5,2)");
                entity.Property(e => e.BaseAmount).HasColumnType("decimal(18,4)");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,4)");

                entity.HasOne(e => e.DocumentItem)
                      .WithMany(i => i.Retentions)
                      .HasForeignKey(e => e.DocumentItemId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DocumentGeneralRetention>(entity =>
            {
                entity.ToTable("DocumentGeneralRetentions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TaxCategory).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Rate).HasColumnType("decimal(6,3)");

                entity.HasOne(e => e.Document)
                      .WithMany(d => d.GeneralRetentions)
                      .HasForeignKey(e => e.DocumentId)
                      .OnDelete(DeleteBehavior.Cascade);
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
                    
                    // Documentos Equivalentes — DianCode = InvoiceTypeCode real según el Anexo Técnico
                    // v1.0 (Resolución 000165 de 2023, numeral 16.3: docs/dian-doc-equivalente/). Los
                    // valores previos (06-16) eran inventados y no existen en el catálogo real de la
                    // DIAN; se corrigen aquí a los códigos de dos dígitos que sí usa la DIAN.
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000011"), Code = "DE-POS", Name = "Doc. Equivalente - Tiquete POS", Description = "Tiquete de máquina registradora POS", DianCode = "20" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000012"), Code = "DE-CINE", Name = "Doc. Equivalente - Cine", Description = "Boleta de ingreso a cine", DianCode = "25" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000013"), Code = "DE-PASAJEROS", Name = "Doc. Equivalente - Transporte Pasajeros", Description = "Tiquete de transporte terrestre de pasajeros", DianCode = "35" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000014"), Code = "DE-EXTRACTO", Name = "Doc. Equivalente - Extracto", Description = "Extracto expedido por sociedades financieras y fondos", DianCode = "45" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000015"), Code = "DE-AEREO", Name = "Doc. Equivalente - Transporte Aéreo", Description = "Tiquete de transporte aéreo de pasajeros", DianCode = "50" },
                    // Juegos localizados y no localizados comparten un único código DIAN (30) — DE-AZAR
                    // ya no es un tipo aparte, se deja apuntando al mismo código por compatibilidad.
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000016"), Code = "DE-JUEGOSLOC", Name = "Doc. Equivalente - Juegos Localizados", Description = "Documento en juegos localizados y no localizados", DianCode = "30" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000017"), Code = "DE-AZAR", Name = "Doc. Equivalente - Suerte y Azar", Description = "Boletas en juegos de suerte y azar (mismo código DIAN que juegos localizados)", DianCode = "30" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000018"), Code = "DE-PEAJE", Name = "Doc. Equivalente - Peajes", Description = "Cobro de peajes", DianCode = "40" },
                    // Bolsa de valores y bolsa agropecuaria también comparten un único código (55).
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000019"), Code = "DE-BOLSA", Name = "Doc. Equivalente - Bolsa de Valores", Description = "Liquidación de operaciones Bolsa de Valores", DianCode = "55" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000020"), Code = "DE-AGRO", Name = "Doc. Equivalente - Bolsa Agropecuaria", Description = "Operaciones bolsa agropecuaria y otros commodities (mismo código DIAN que bolsa de valores)", DianCode = "55" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000021"), Code = "DE-SERVICIOSP", Name = "Doc. Equivalente - Servicios Públicos", Description = "Servicios públicos domiciliarios", DianCode = "60" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000022"), Code = "DE-ESPECTACULOS", Name = "Doc. Equivalente - Espectáculos Públicos", Description = "Ingreso a espectáculos públicos", DianCode = "27" },
                    // La Nota de Ajuste tiene dos códigos DIAN separados (93=débito, 94=crédito), no
                    // uno solo como estaba antes.
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000023"), Code = "DE-AJUSTE-CREDITO", Name = "Nota de Ajuste (Crédito) - Doc. Equivalente", Description = "Nota de ajuste tipo crédito para documentos equivalentes", DianCode = "94" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000032"), Code = "DE-AJUSTE-DEBITO", Name = "Nota de Ajuste (Débito) - Doc. Equivalente", Description = "Nota de ajuste tipo débito para documentos equivalentes", DianCode = "93" },
                    
                    // Documento Soporte
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000024"), Code = "DS", Name = "Doc. Soporte - Adquisiciones a No Obligados", Description = "Documento soporte", DianCode = "05" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000025"), Code = "DS-AJUSTE", Name = "Nota de Ajuste - Doc. Soporte", Description = "Ajuste a documento soporte", DianCode = "95" },
                    
                    // Nómina
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000026"), Code = "NE-PAGO", Name = "Nómina Electrónica", Description = "Pago de nómina electrónica", DianCode = "102" },
                    new DocumentType { Id = Guid.Parse("00000000-0000-0000-0000-000000000027"), Code = "NE-AJUSTE", Name = "Nota de Ajuste - Nómina Electrónica", Description = "Ajuste de nómina electrónica", DianCode = "103" }
                );
            });

            modelBuilder.Entity<Integrator>(entity =>
            {
                entity.ToTable("Integrators");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(30);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Nit).HasMaxLength(50);

                entity.HasData(
                    new Integrator { Id = Guid.Parse("00000000-0000-0000-0000-000000000101"), Code = "NATIVE", Name = "Emisión directa DIAN", Nit = "", Kind = IntegratorKind.DirectDian, IsActive = true },
                    new Integrator { Id = Guid.Parse("00000000-0000-0000-0000-000000000102"), Code = "DATAICO", Name = "Dataico S.A.S.", Nit = "900.XXX.XXX-X", Kind = IntegratorKind.ThirdPartyIntegrator, IsActive = true }
                );
            });

            modelBuilder.Entity<ClientIntegratorAssignment>(entity =>
            {
                entity.ToTable("ClientIntegratorAssignments");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);

                // A lo sumo un tramo abierto (EffectiveTo NULL) por Client — el que representa la
                // asignación vigente. SQL Server no considera iguales dos NULL en un índice único
                // filtrado, así que esto sí impide dos filas abiertas para el mismo ClientId.
                entity.HasIndex(e => e.ClientId)
                      .HasFilter("[EffectiveTo] IS NULL")
                      .IsUnique();
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

            modelBuilder.Entity<TenantUser>(entity =>
            {
                // El correo es la identidad real: el login del portal de tenants no pide slug (a
                // diferencia del de clientes) y resuelve solo por email, así que dos TenantUser
                // con el mismo correo harían el login impredecible — entraría a uno arbitrario de
                // los dos tenants. Antes esto se garantizaba solo con un chequeo en el controlador
                // de alta; con el índice queda garantizado también a nivel de base.
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<TenantUserAssignment>(entity =>
            {
                entity.ToTable("TenantUserAssignments");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.TenantUser)
                      .WithMany()
                      .HasForeignKey(e => e.TenantUserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Restrict, no Cascade: Tenants ya llega a esta tabla por dos caminos (borrar un
                // Tenant borra en cascada sus TenantUsers, que a su vez borrarían en cascada sus
                // TenantUserAssignments) — un segundo camino de cascada directo Tenants ->
                // TenantUserAssignments es justo lo que SQL Server rechaza como "multiple cascade
                // paths". No es una pérdida real: el borrado físico de tenants no existe todavía
                // en el sistema (ver BACKLOG.md), así que esta ruta no se ejercita hoy.
                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Un usuario no puede tener dos asignaciones al mismo tenant.
                entity.HasIndex(e => new { e.TenantUserId, e.TenantId }).IsUnique();
            });

            modelBuilder.Entity<TenantUserPricing>(entity =>
            {
                entity.ToTable("TenantUserPricings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PricePerUser).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).HasMaxLength(10);

                entity.HasOne(e => e.Tenant)
                      .WithOne(t => t.UserPricing)
                      .HasForeignKey<TenantUserPricing>(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.TenantId).IsUnique();
            });

            modelBuilder.Entity<PrepaidPackage>(entity =>
            {
                entity.ToTable("PrepaidPackages");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountedPricePerUser).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.PrepaidPackages)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TenantPrepaidBag>(entity =>
            {
                entity.ToTable("TenantPrepaidBags");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RemainingBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountedPricePerUser).HasColumnType("decimal(18,2)");
                entity.Property(e => e.AmountPaid).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Tenant)
                      .WithMany(t => t.PrepaidBags)
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Package)
                      .WithMany(p => p.Bags)
                      .HasForeignKey(e => e.PackageId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SuperadminUser>(entity =>
            {
                entity.ToTable("SuperadminUsers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(150);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.PasswordHash).IsRequired();
            });

            modelBuilder.Entity<DocumentTemplate>(entity =>
            {
                entity.ToTable("DocumentTemplates");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.RepxTemplateKey).IsRequired().HasMaxLength(150);
                
                entity.HasOne(e => e.DocumentType)
                      .WithMany()
                      .HasForeignKey(e => e.DocumentTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PreviousVersion)
                      .WithMany()
                      .HasForeignKey(e => e.PreviousVersionId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ClonedFrom)
                      .WithMany()
                      .HasForeignKey(e => e.ClonedFromId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClientDocumentSetting>(entity =>
            {
                entity.ToTable("ClientDocumentSettings");
                entity.HasKey(e => e.Id);
                
                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);
                      
                entity.HasOne(e => e.DocumentType)
                      .WithMany()
                      .HasForeignKey(e => e.DocumentTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.SelectedTemplate)
                      .WithMany()
                      .HasForeignKey(e => e.SelectedTemplateId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(250);
                entity.Property(e => e.IdentificationNumber).IsRequired().HasMaxLength(50);
                
                entity.HasOne(e => e.Client)
                      .WithMany(c => c.Customers)
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);
                      
                entity.HasIndex(e => new { e.ClientId, e.IdentificationNumber }).IsUnique();
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(250);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,4)");
                entity.Property(e => e.IvaRate).HasColumnType("decimal(5,2)");

                entity.HasOne(e => e.Client)
                      .WithMany(c => c.Products)
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.ClientId, e.Code }).IsUnique();
            });

            modelBuilder.Entity<ProductTax>(entity =>
            {
                entity.ToTable("ProductTaxes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TaxCategory).IsRequired().HasMaxLength(30);
                entity.Property(e => e.Rate).HasColumnType("decimal(5,2)");

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.Taxes)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TariffTier>(entity =>
            {
                entity.ToTable("TariffTiers");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.PricePerDocument).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasData(
                    new TariffTier { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Nivel 1", MinDocuments = 1, MaxDocuments = 2000, PricePerDocument = 70m },
                    new TariffTier { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Nivel 2", MinDocuments = 2001, MaxDocuments = 5000, PricePerDocument = 50m },
                    new TariffTier { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Nivel 3", MinDocuments = 5001, MaxDocuments = 10000, PricePerDocument = 40m },
                    new TariffTier { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Nivel 4", MinDocuments = 10001, MaxDocuments = 100000, PricePerDocument = 30m },
                    new TariffTier { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Nivel 5", MinDocuments = 100001, MaxDocuments = null, PricePerDocument = 20m }
                );
            });

            modelBuilder.Entity<TenantIntegratorBilling>(entity =>
            {
                entity.ToTable("TenantIntegratorBillings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PricePerUser).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.TenantId, e.IntegratorId }).IsUnique();
            });

            modelBuilder.Entity<ClientIntegratorBilling>(entity =>
            {
                entity.ToTable("ClientIntegratorBillings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.PricePerDocument).HasColumnType("decimal(18,2)");
                entity.Property(e => e.PricePerUser).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.ClientId, e.IntegratorId }).IsUnique();
            });

            modelBuilder.Entity<ClientPrepaidPackage>(entity =>
            {
                entity.ToTable("ClientPrepaidPackages");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountedPricePerDocument).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClientPrepaidBag>(entity =>
            {
                entity.ToTable("ClientPrepaidBags");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RemainingBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountedPricePerDocument).HasColumnType("decimal(18,2)");
                entity.Property(e => e.AmountPaid).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Package)
                      .WithMany(p => p.Bags)
                      .HasForeignKey(e => e.PackageId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Integrator)
                      .WithMany()
                      .HasForeignKey(e => e.IntegratorId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ClientEnabledDocumentType>(entity =>
            {
                entity.ToTable("ClientEnabledDocumentTypes");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DocumentType)
                      .WithMany()
                      .HasForeignKey(e => e.DocumentTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.ClientId, e.DocumentTypeId }).IsUnique();
            });

            modelBuilder.Entity<ClientEnabledRetentionConcept>(entity =>
            {
                entity.ToTable("ClientEnabledRetentionConcepts");
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.RetentionConcept)
                      .WithMany()
                      .HasForeignKey(e => e.RetentionConceptId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.ClientId, e.RetentionConceptId }).IsUnique();
            });

            modelBuilder.Entity<DeveloperUser>(entity =>
            {
                entity.ToTable("DeveloperUsers");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();

                entity.HasOne(e => e.Tenant)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DianMunicipality>(entity =>
            {
                entity.ToTable("DianMunicipalities");
                entity.HasKey(e => e.Code);
                entity.Property(e => e.Code).HasMaxLength(5);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.DepartmentCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.DepartmentName).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.DepartmentCode);
            });

            modelBuilder.Entity<DataicoTaxCatalogItem>(entity =>
            {
                entity.ToTable("DataicoTaxCatalogItems");
                entity.HasKey(e => e.Id);
                // Algunos códigos de nómina (WorkerType, PayrollPaymentMeans) son bastante largos
                // (ej. "TRABAJADOR_DEPENDIENTE_DE_ENTIDAD_BENEFICIARIA_DEL_SISTEMA_GENERAL_DE_..."),
                // así que el límite es más generoso que un simple código de impuesto de 2-3 palabras.
                entity.Property(e => e.Category).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Rate).HasColumnType("decimal(6,3)");

                // Precargados con lo que ya venía escrito a mano en el frontend, más los catálogos
                // completos confirmados contra C:\FEL (Postman real de Dataico + la plantilla de
                // nómina), para que el catálogo no arranque vacío.
                var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                entity.HasData(
                    // Deprecadas: eran solo la categoría sin tarifa, así que el cliente terminaba
                    // escribiendo el % a mano. Se dejan inactivas (no se borran, TaxCategory ya
                    // quedó grabado como texto plano en retenciones históricas) y se reemplazan por
                    // combinaciones categoría+tarifa concretas más abajo.
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Category = "RET_FUENTE", Name = "Retención en la Fuente", Kind = TaxCatalogKind.Retention, IsActive = false, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Category = "RET_ICA", Name = "Retención de ICA", Kind = TaxCatalogKind.Retention, IsActive = false, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Category = "RET_IVA", Name = "Retención de IVA", Kind = TaxCatalogKind.Retention, IsActive = false, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    // Combinaciones categoría+tarifa concretas (reemplazan a las de arriba). Las de
                    // RET_ICA son solo un punto de partida (tarifas típicas de Bogotá) — el % real de
                    // ICA depende del municipio y la actividad económica de cada cliente, así que
                    // Superadmin debe ajustar/agregar las que apliquen a cada caso real.
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000001"), Category = "RET_FUENTE", Rate = 2.5m, Name = "Compras generales (2.5%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000002"), Category = "RET_FUENTE", Rate = 4m, Name = "Servicios generales (4%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000003"), Category = "RET_FUENTE", Rate = 11m, Name = "Servicios profesionales / honorarios (11%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000004"), Category = "RET_ICA", Rate = 0.414m, Name = "Actividad industrial Bogotá (0.414%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000005"), Category = "RET_ICA", Rate = 0.966m, Name = "Actividad de servicios Bogotá (0.966%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000006"), Category = "RET_IVA", Rate = 15m, Name = "Estándar (15%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("11000000-0000-0000-0000-000000000007"), Category = "RET_IVA", Rate = 20m, Name = "Grandes contribuyentes (20%)", Kind = TaxCatalogKind.Retention, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Category = "IMP_CONSUMO", Name = "Impuesto al Consumo", Kind = TaxCatalogKind.OtherTax, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Category = "IMP_CONSUMO_LICOR", Name = "Impuesto al Consumo de Licores", Kind = TaxCatalogKind.OtherTax, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Category = "IMP_BOLSA_PLASTICA", Name = "Impuesto a la Bolsa Plástica", Kind = TaxCatalogKind.OtherTax, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    // Tarifas de IVA (DIAN solo reconoce 19% y 5% como tarifas "Gravado"; 0% se modela con el tratamiento Exento).
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000007"), Category = "19", Name = "IVA General (19%)", Kind = TaxCatalogKind.IvaRate, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000008"), Category = "5", Name = "IVA Reducido (5%)", Kind = TaxCatalogKind.IvaRate, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    // Plazos de pago comerciales más usados en Colombia.
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000009"), Category = "0", Name = "Contado (0 días)", Kind = TaxCatalogKind.PaymentTerm, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-00000000000a"), Category = "15", Name = "15 días", Kind = TaxCatalogKind.PaymentTerm, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-00000000000b"), Category = "30", Name = "30 días", Kind = TaxCatalogKind.PaymentTerm, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-00000000000c"), Category = "45", Name = "45 días", Kind = TaxCatalogKind.PaymentTerm, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-00000000000d"), Category = "60", Name = "60 días", Kind = TaxCatalogKind.PaymentTerm, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-00000000000e"), Category = "90", Name = "90 días", Kind = TaxCatalogKind.PaymentTerm, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    // Medios de pago (los mismos que ya venían sugeridos a mano en el frontend).
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-00000000000f"), Category = "EFECTIVO", Name = "Efectivo", Kind = TaxCatalogKind.PaymentMeans, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000010"), Category = "TRANSFERENCIA", Name = "Transferencia Bancaria", Kind = TaxCatalogKind.PaymentMeans, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000011"), Category = "DEBIT_CARD", Name = "Tarjeta Débito", Kind = TaxCatalogKind.PaymentMeans, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("10000000-0000-0000-0000-000000000012"), Category = "CREDIT_CARD", Name = "Tarjeta Crédito", Kind = TaxCatalogKind.PaymentMeans, IsActive = true, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    // Tipo de trabajador (Nómina Electrónica) — catálogo completo confirmado contra la
                    // hoja POSIBLE_LISTA_DE_VALORES de la plantilla real de nómina.
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Category = "DEPENDIENTE", Name = "Dependiente", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Category = "PROFESOR_DE_ESTABLECIMIENTO_PARTICULAR", Name = "Profesor De Establecimiento Particular", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Category = "PRE_PENSIONADO_CON_APORTE_VOLUNTARIO_A_SALUD", Name = "Pre Pensionado Con Aporte Voluntario A Salud", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Category = "SERVICIO_DOMESTICO", Name = "Servicio Domestico", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Category = "APRENDICES_DEL_SENA_EN_ETAPA_LECTIVA", Name = "Aprendices Del Sena En Etapa Lectiva", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), Category = "COOPERADOS_O_PRE_COOPERATIVAS_DE_TRABAJO_ASOCIADO", Name = "Cooperados O Pre Cooperativas De Trabajo Asociado", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), Category = "ESTUDIANTES_DE_PRACTICAS_LABORALES_EN_EL_SECTOR_PUBLICO", Name = "Estudiantes De Practicas Laborales En El Sector Publico", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000008"), Category = "TRABAJADOR_DEPENDIENTE_DE_ENTIDAD_BENEFICIARIA_DEL_SISTEMA_GENERAL_DE_PARTICIPACIONES_APORTES_PATRONALES", Name = "Trabajador Dependiente De Entidad Beneficiaria Del Sistema General De Participaciones Aportes Patronales", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000009"), Category = "FUNCIONARIOS_PUBLICOS_SIN_TOPE_MAXIMO_DE_IBC", Name = "Funcionarios Publicos Sin Tope Maximo De Ibc", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000010"), Category = "MADRE_COMUNITARIA", Name = "Madre Comunitaria", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000011"), Category = "ESTUDIANTES_DE_POSTGRADO_EN_SALUD", Name = "Estudiantes De Postgrado En Salud", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000012"), Category = "DEPENDIENTE_ENTIDADES_O_UNIVERSIDADES_PUBLICAS_CON_REGIMEN_ESPECIAL_EN_SALUD", Name = "Dependiente Entidades O Universidades Publicas Con Regimen Especial En Salud", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000013"), Category = "PRE_PENSIONADO_DE_ENTIDAD_EN_LIQUIDACION", Name = "Pre Pensionado De Entidad En Liquidacion", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000014"), Category = "ESTUDIANTES_APORTES_SOLO_RIESGOS_LABORALES", Name = "Estudiantes Aportes Solo Riesgos Laborales", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000015"), Category = "TRABAJADOR_DE_TIEMPO_PARCIAL", Name = "Trabajador De Tiempo Parcial", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000016"), Category = "APRENDICES_DEL_SENA_EN_ETAPA_PRODUCTIVA", Name = "Aprendices Del Sena En Etapa Productiva", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000017"), Category = "APRENDICES_DEL_SENA_EN_ETAPA_PRODUCTIVA_REFORMA_2025", Name = "Aprendices Del Sena En Etapa Productiva Reforma 2025", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("20000000-0000-0000-0000-000000000018"), Category = "APRENDICES_DEL_SENA_EN_ETAPA_LECTIVA_REFORMA_2025", Name = "Aprendices Del Sena En Etapa Lectiva Reforma 2025", Kind = TaxCatalogKind.WorkerType, IsActive = true, CreatedAt = seedDate },
                    // Tipo de contrato (Nómina Electrónica).
                    new DataicoTaxCatalogItem { Id = Guid.Parse("30000000-0000-0000-0000-000000000001"), Category = "TERMINO_FIJO", Name = "Termino Fijo", Kind = TaxCatalogKind.ContractType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("30000000-0000-0000-0000-000000000002"), Category = "TERMINO_INDEFINIDO", Name = "Termino Indefinido", Kind = TaxCatalogKind.ContractType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("30000000-0000-0000-0000-000000000003"), Category = "OBRA_LABOR", Name = "Obra Labor", Kind = TaxCatalogKind.ContractType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("30000000-0000-0000-0000-000000000004"), Category = "APRENDIZAJE", Name = "Aprendizaje", Kind = TaxCatalogKind.ContractType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("30000000-0000-0000-0000-000000000005"), Category = "PRACTICAS_PASANTIAS", Name = "Practicas Pasantias", Kind = TaxCatalogKind.ContractType, IsActive = true, CreatedAt = seedDate },
                    // Medio de pago del empleado (Nómina Electrónica) — catálogo distinto del de Facturas/Documentos Soporte.
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Category = "CTX", Name = "Ctx", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Category = "VALES", Name = "Vales", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), Category = "NOTA_PROMISORIA_FIRMADA_PRO_EL_BANCO", Name = "Nota Promisoria Firmada Pro El Banco", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), Category = "GIRO_URGENTE", Name = "Giro Urgente", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000005"), Category = "CONCENTRACION_EFECTIVO_AHORROS_/_DESEMBOLSO_CREDITO_CCD", Name = "Concentración Efectivo/Ahorros - Desembolso Crédito CCD", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000006"), Category = "REVERSION_CREDITO_AHORRO", Name = "Reversion Credito Ahorro", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000007"), Category = "DEBITO_CTX", Name = "Debito Ctx", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000008"), Category = "NOTA_RETIRO", Name = "Nota Retiro", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000009"), Category = "NOTA_CAMBIARIA", Name = "Nota Cambiaria", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000010"), Category = "NOTA_RETIRO_TERCERO", Name = "Nota Retiro Tercero", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000011"), Category = "EFECTIVO", Name = "Efectivo", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000012"), Category = "CHEQUE_LOCAL_TRAFERIBLE", Name = "Cheque Local Traferible", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000013"), Category = "NOTA_BANCARIA_TRANFERIBLE", Name = "Nota Bancaria Tranferible", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000014"), Category = "BOOKENTRY_DEBITO", Name = "Bookentry Debito", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000015"), Category = "NOTA_PROMISORIA_FIRMADA_POR_EL_ACREEDOR_AVALADA_POR_UN_TERCERO", Name = "Nota Promisoria Firmada Por El Acreedor Avalada Por Un Tercero", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000016"), Category = "PAGO_TESORERIA_URGENTE", Name = "Pago Tesoreria Urgente", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000017"), Category = "REVERSION_CREDITO_DE_DEMANDA_ACH", Name = "Reversion Credito De Demanda Ach", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000018"), Category = "ACUERDO_MUTUO", Name = "Acuerdo Mutuo", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000019"), Category = "TARJETA_CREDITO", Name = "Tarjeta Credito", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000020"), Category = "BONOS", Name = "Bonos", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000021"), Category = "DESEMBOLSO_PLUS_DEBITO", Name = "Desembolso Plus Debito", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000022"), Category = "CREDITO_AHORRO", Name = "Credito Ahorro", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000023"), Category = "BOOKENTRY_CREDITO", Name = "Bookentry Credito", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000024"), Category = "METODO_DE_PAGO_SOLICITADO_NO_USUADO", Name = "Metodo De Pago Solicitado No Usuado", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000025"), Category = "TELEX_ESTANDAR_BANCARIO_FRANCES", Name = "Telex Estandar Bancario Frances", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000026"), Category = "CREDITO_ACH", Name = "Credito Ach", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000027"), Category = "CLEARING_ENTRE_PARTNERS", Name = "Clearing Entre Partners", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000028"), Category = "DESEMBOLSO_DEBITO", Name = "Desembolso Debito", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000029"), Category = "DEBITO_DE_DEMANDA_ACH", Name = "Debito De Demanda Ach", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000030"), Category = "INSTRUMENTO_NO_DEFINIDO", Name = "Instrumento No Definido", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000031"), Category = "TRANSFERENCIA_DEBITO_INTERBANCARIO", Name = "Transferencia Debito Interbancario", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000032"), Category = "DESEMBOLSO_CREDITO_PLUS", Name = "Desembolso Credito Plus", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000033"), Category = "PAGO_DEPOSITO_PRE_ACORDADO", Name = "Pago Deposito Pre Acordado", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000034"), Category = "NOTA_PROMISORIA_FIRMADA_POR_UN_BANCO_AVALADA_POR_OTRO_BANCO", Name = "Nota Promisoria Firmada Por Un Banco Avalada Por Otro Banco", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000035"), Category = "NOTA_PROMISORIA_FIRMADA", Name = "Nota Promisoria Firmada", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000036"), Category = "CHEQUE_BANCARIO", Name = "Cheque Bancario", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000037"), Category = "CHEQUE", Name = "Cheque", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000038"), Category = "NOTA_PROMISORIA", Name = "Nota Promisoria", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000039"), Category = "POSTGIRO", Name = "Postgiro", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000040"), Category = "RETIRO_DE_NOTA_POR_EL_POR_EL_ACREEDOR_SOBRE_UN_BANCO", Name = "Retiro De Nota Por El Por El Acreedor Sobre Un Banco", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000041"), Category = "PAGO_COMERCIAL_URGENTE", Name = "Pago Comercial Urgente", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000042"), Category = "RETIRO_DE_NOTA_POR_EL_ACREEDOR_AVALADA_POR_OTRO_BANCO", Name = "Retiro De Nota Por El Acreedor Avalada Por Otro Banco", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000043"), Category = "DEBITO_ACH", Name = "Debito Ach", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000044"), Category = "TARJETA_DEBITO", Name = "Tarjeta Debito", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000045"), Category = "NOTA_PROMISORIA_FIRMADA_ACREEDOR", Name = "Nota Promisoria Firmada Acreedor", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000046"), Category = "PROYECTO_BANCARIO", Name = "Proyecto Bancario", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000047"), Category = "NOTA_PROMISORIA_BANCO", Name = "Nota Promisoria Banco", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000048"), Category = "PAGO_NEGOCIO_CORPORATIVO_AHORROS_CREDITO_CTP", Name = "Pago Negocio Corporativo Ahorros Credito Ctp", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000049"), Category = "PAGO_NEGOCIO_CORPORATIVO_AHORROS_DEBITO_CTP", Name = "Pago Negocio Corporativo Ahorros Debito Ctp", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000050"), Category = "CONSIGNACION_BANCARIA", Name = "Consignacion Bancaria", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000051"), Category = "CHEQUE_LOCAL", Name = "Cheque Local", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000052"), Category = "CREDITO_CTP", Name = "Credito Ctp", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("40000000-0000-0000-0000-000000000053"), Category = "GIRO_REFERENCIADO", Name = "Giro Referenciado", Kind = TaxCatalogKind.PayrollPaymentMeans, IsActive = true, CreatedAt = seedDate },
                    // Nivel tributario y régimen del tercero (todos los partidos: Cliente/Proveedor/Empleado).
                    new DataicoTaxCatalogItem { Id = Guid.Parse("50000000-0000-0000-0000-000000000001"), Category = "SIMPLIFICADO", Name = "Régimen Simplificado", Kind = TaxCatalogKind.TaxLevelCode, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("50000000-0000-0000-0000-000000000002"), Category = "COMUN", Name = "Régimen Común", Kind = TaxCatalogKind.TaxLevelCode, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("50000000-0000-0000-0000-000000000003"), Category = "RESPONSABLE_DE_IVA", Name = "Responsable de IVA", Kind = TaxCatalogKind.TaxLevelCode, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("50000000-0000-0000-0000-000000000004"), Category = "NO_RESPONSABLE_DE_IVA", Name = "No Responsable de IVA", Kind = TaxCatalogKind.TaxLevelCode, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("60000000-0000-0000-0000-000000000001"), Category = "SIMPLE", Name = "Régimen Simple", Kind = TaxCatalogKind.Regimen, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("60000000-0000-0000-0000-000000000002"), Category = "ORDINARIO", Name = "Régimen Ordinario", Kind = TaxCatalogKind.Regimen, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("60000000-0000-0000-0000-000000000003"), Category = "AUTORRETENEDOR", Name = "Autorretenedor", Kind = TaxCatalogKind.Regimen, IsActive = true, CreatedAt = seedDate },
                    // Tipo de cuenta bancaria (empleados).
                    new DataicoTaxCatalogItem { Id = Guid.Parse("70000000-0000-0000-0000-000000000001"), Category = "AHORROS", Name = "Ahorros", Kind = TaxCatalogKind.AccountType, IsActive = true, CreatedAt = seedDate },
                    new DataicoTaxCatalogItem { Id = Guid.Parse("70000000-0000-0000-0000-000000000002"), Category = "CORRIENTE", Name = "Corriente", Kind = TaxCatalogKind.AccountType, IsActive = true, CreatedAt = seedDate }
                );
            });
        }
    }
}
