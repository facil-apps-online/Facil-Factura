-- Reconstruye facturas ya aceptadas por Dataico que faltan en FelDb.
-- Solo copia datos locales; no llama a Dataico ni publica documentos.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now datetime2 = SYSUTCDATETIME();
DECLARE @crSource uniqueidentifier = '2089EE20-17AD-495B-9E14-4CC8D84974D0';
DECLARE @kaSource uniqueidentifier = '566B48C2-7B59-4843-B5EA-6DF23E2FA56C';
DECLARE @crTarget uniqueidentifier = 'B6C4D9E3-5B1C-4A80-9A5F-0F9C5F1F3262';
DECLARE @kaTarget uniqueidentifier = 'B6C4D9E3-5B1C-4A80-9A5F-0F9C5F1F3252';
DECLARE @crCufe nvarchar(max) = '3b4cac536d136b6e7ee680fb9419e13480b3c0c7953d829658a24c1dce75912738da2583a2f0d11ebae7480044c68755';
DECLARE @crDataicoId nvarchar(max) = '01a1890e-ed5a-8ccc-b2f4-08819a2ec00b';
DECLARE @kaCufe nvarchar(max) = 'fd8ffc93ff3e4c6b799ecba8c14adb7eaf547a8357ce5c6faefbf47d64a24b6f32b0ea843100755a4e9d89c45a2ef4a8';
DECLARE @kaDataicoId nvarchar(max) = '01a187c7-5b07-8605-8ed7-0ee48db9a6d0';

IF EXISTS (SELECT 1 FROM Documents WHERE Id IN (@crTarget,@kaTarget))
    THROW 51000, 'Documento de reconstruccion ya existe por Id.', 1;
IF EXISTS (SELECT 1 FROM Documents WHERE ClientId=(SELECT ClientId FROM Documents WHERE Id=@crSource) AND Prefix='FE' AND Number='3262')
    THROW 51001, 'FE3262 ya existe localmente.', 1;
IF EXISTS (SELECT 1 FROM Documents WHERE ClientId=(SELECT ClientId FROM Documents WHERE Id=@kaSource) AND Prefix='FE' AND Number='3252')
    THROW 51002, 'FE3252 ya existe localmente.', 1;

INSERT INTO Documents
(Id,ClientId,TrackingId,TypeCode,Number,Cufe,Status,DianResponseCode,DianResponseMessage,XmlUrl,PdfUrl,CreatedAt,ProcessedAt,PriceCharged,DocumentTypeId,UsedTemplateId,CustomerId,Notes,SectorExtensionData,Subtotal,TaxAmount,TotalAmount,ReferenceConcept,ReferenceDocumentId,PaymentMeans,PaymentMeansType,GeneralDiscountAmount,GeneralDiscountReason,PaymentTermDays,PurchaseOrderReference,ResolutionId,IssueDate,DataicoDocumentId,IntegratorId,GeneralChargeAmount,GeneralChargeReason,QrCode,DianTrackId,DiscrepancyResponseCode,Prefix,BranchId,Currency,ExchangeRate,ExchangeRateDate,DeliveryTermsCode,DeliveryTermsName,RoundingAdjustmentAmount,RoundingAdjustmentReason,NoteNumberingId,SentToSandbox)
SELECT @crTarget,ClientId,CONCAT('reconciled-dataico-',CONVERT(varchar(36),@crTarget)),TypeCode,'3262',@crCufe,'APPROVED',DianResponseCode,'Documento reconstruido desde Dataico; no se reenviara. Confirmado DIAN_ACEPTADO.',NULL,NULL,@now,@now,PriceCharged,DocumentTypeId,UsedTemplateId,CustomerId,Notes,SectorExtensionData,Subtotal,TaxAmount,TotalAmount,ReferenceConcept,ReferenceDocumentId,PaymentMeans,PaymentMeansType,GeneralDiscountAmount,GeneralDiscountReason,PaymentTermDays,PurchaseOrderReference,ResolutionId,IssueDate,@crDataicoId,IntegratorId,GeneralChargeAmount,GeneralChargeReason,NULL,NULL,DiscrepancyResponseCode,Prefix,BranchId,Currency,ExchangeRate,ExchangeRateDate,DeliveryTermsCode,DeliveryTermsName,RoundingAdjustmentAmount,RoundingAdjustmentReason,NoteNumberingId,0
FROM Documents WHERE Id=@crSource;

INSERT INTO Documents
(Id,ClientId,TrackingId,TypeCode,Number,Cufe,Status,DianResponseCode,DianResponseMessage,XmlUrl,PdfUrl,CreatedAt,ProcessedAt,PriceCharged,DocumentTypeId,UsedTemplateId,CustomerId,Notes,SectorExtensionData,Subtotal,TaxAmount,TotalAmount,ReferenceConcept,ReferenceDocumentId,PaymentMeans,PaymentMeansType,GeneralDiscountAmount,GeneralDiscountReason,PaymentTermDays,PurchaseOrderReference,ResolutionId,IssueDate,DataicoDocumentId,IntegratorId,GeneralChargeAmount,GeneralChargeReason,QrCode,DianTrackId,DiscrepancyResponseCode,Prefix,BranchId,Currency,ExchangeRate,ExchangeRateDate,DeliveryTermsCode,DeliveryTermsName,RoundingAdjustmentAmount,RoundingAdjustmentReason,NoteNumberingId,SentToSandbox)
SELECT @kaTarget,ClientId,CONCAT('reconciled-dataico-',CONVERT(varchar(36),@kaTarget)),TypeCode,'3252',@kaCufe,'APPROVED',DianResponseCode,'Documento reconstruido desde Dataico; no se reenviara. Confirmado DIAN_ACEPTADO.',NULL,NULL,@now,@now,PriceCharged,DocumentTypeId,UsedTemplateId,CustomerId,Notes,SectorExtensionData,Subtotal,TaxAmount,TotalAmount,ReferenceConcept,ReferenceDocumentId,PaymentMeans,PaymentMeansType,GeneralDiscountAmount,GeneralDiscountReason,PaymentTermDays,PurchaseOrderReference,ResolutionId,IssueDate,@kaDataicoId,IntegratorId,GeneralChargeAmount,GeneralChargeReason,NULL,NULL,DiscrepancyResponseCode,Prefix,BranchId,Currency,ExchangeRate,ExchangeRateDate,DeliveryTermsCode,DeliveryTermsName,RoundingAdjustmentAmount,RoundingAdjustmentReason,NoteNumberingId,0
FROM Documents WHERE Id=@kaSource;

DECLARE @crSourceItem uniqueidentifier=(SELECT TOP 1 Id FROM DocumentItems WHERE DocumentId=@crSource);
DECLARE @kaSourceItem uniqueidentifier=(SELECT TOP 1 Id FROM DocumentItems WHERE DocumentId=@kaSource);
DECLARE @crTargetItem uniqueidentifier=NEWID();
DECLARE @kaTargetItem uniqueidentifier=NEWID();

INSERT INTO DocumentItems
(Id,DocumentId,ProductId,Code,Name,Quantity,UnitPrice,TaxRate,TaxAmount,TotalAmount,SectorExtensionData,IvaTreatment,DiscountRate,RetentionOverridden,UnitOfMeasureAbbreviation,UnitOfMeasureCode,UnitOfMeasureDisplayFormat,LineNumber,MandanteCustomerId,MandanteIdentification,MandanteIdentificationType,MandanteName,BrandName,ModelName,TariffCode)
SELECT @crTargetItem,@crTarget,ProductId,Code,Name,Quantity,UnitPrice,TaxRate,TaxAmount,TotalAmount,SectorExtensionData,IvaTreatment,DiscountRate,RetentionOverridden,UnitOfMeasureAbbreviation,UnitOfMeasureCode,UnitOfMeasureDisplayFormat,LineNumber,MandanteCustomerId,MandanteIdentification,MandanteIdentificationType,MandanteName,BrandName,ModelName,TariffCode
FROM DocumentItems WHERE Id=@crSourceItem;

INSERT INTO DocumentItems
(Id,DocumentId,ProductId,Code,Name,Quantity,UnitPrice,TaxRate,TaxAmount,TotalAmount,SectorExtensionData,IvaTreatment,DiscountRate,RetentionOverridden,UnitOfMeasureAbbreviation,UnitOfMeasureCode,UnitOfMeasureDisplayFormat,LineNumber,MandanteCustomerId,MandanteIdentification,MandanteIdentificationType,MandanteName,BrandName,ModelName,TariffCode)
SELECT @kaTargetItem,@kaTarget,ProductId,Code,Name,Quantity,UnitPrice,TaxRate,TaxAmount,TotalAmount,SectorExtensionData,IvaTreatment,DiscountRate,RetentionOverridden,UnitOfMeasureAbbreviation,UnitOfMeasureCode,UnitOfMeasureDisplayFormat,LineNumber,MandanteCustomerId,MandanteIdentification,MandanteIdentificationType,MandanteName,BrandName,ModelName,TariffCode
FROM DocumentItems WHERE Id=@kaSourceItem;

INSERT INTO DocumentRetentions (Id,DocumentItemId,TaxCategory,Rate,BaseAmount,Amount)
SELECT NEWID(),@crTargetItem,TaxCategory,Rate,BaseAmount,Amount FROM DocumentRetentions WHERE DocumentItemId=@crSourceItem;
INSERT INTO DocumentRetentions (Id,DocumentItemId,TaxCategory,Rate,BaseAmount,Amount)
SELECT NEWID(),@kaTargetItem,TaxCategory,Rate,BaseAmount,Amount FROM DocumentRetentions WHERE DocumentItemId=@kaSourceItem;
INSERT INTO DocumentItemTaxes (Id,DocumentItemId,TaxCategory,Rate,BaseAmount,Amount)
SELECT NEWID(),@crTargetItem,TaxCategory,Rate,BaseAmount,Amount FROM DocumentItemTaxes WHERE DocumentItemId=@crSourceItem;
INSERT INTO DocumentItemTaxes (Id,DocumentItemId,TaxCategory,Rate,BaseAmount,Amount)
SELECT NEWID(),@kaTargetItem,TaxCategory,Rate,BaseAmount,Amount FROM DocumentItemTaxes WHERE DocumentItemId=@kaSourceItem;
INSERT INTO DocumentGeneralRetentions (Id,DocumentId,TaxCategory,Rate)
SELECT NEWID(),@crTarget,TaxCategory,Rate FROM DocumentGeneralRetentions WHERE DocumentId=@crSource;
INSERT INTO DocumentGeneralRetentions (Id,DocumentId,TaxCategory,Rate)
SELECT NEWID(),@kaTarget,TaxCategory,Rate FROM DocumentGeneralRetentions WHERE DocumentId=@kaSource;
INSERT INTO DocumentObservations (Id,DocumentId,Position,Text)
SELECT NEWID(),@crTarget,Position,Text FROM DocumentObservations WHERE DocumentId=@crSource;
INSERT INTO DocumentObservations (Id,DocumentId,Position,Text)
SELECT NEWID(),@kaTarget,Position,Text FROM DocumentObservations WHERE DocumentId=@kaSource;

COMMIT TRANSACTION;
SELECT d.Number,d.Status,d.Cufe,d.DataicoDocumentId,
       (SELECT COUNT(*) FROM DocumentItems i WHERE i.DocumentId=d.Id) AS Items
FROM Documents d WHERE d.Id IN (@crTarget,@kaTarget);
