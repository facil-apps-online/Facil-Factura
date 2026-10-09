import type { Page } from '@playwright/test';

// Datos deliberadamente largos: nombres de empresa, correos y claves sin espacios son lo que más
// rompe los diseños (ver "contenido extremo" del plan responsive).
export const LONG_NAME = 'Comercializadora Internacional de Productos Agroindustriales y Servicios Profesionales del Caribe S.A.S.';
export const LONG_EMAIL = 'nombre.muy.largo.de.usuario.corporativo@dominio-extremadamente-largo-de-prueba.com.co';
export const LONG_TOKEN = 'a1b2c3d4e5f6'.repeat(10);

// Logo ancho para probar que el alto manda y el ancho no desborda.
const WIDE_LOGO = 'data:image/svg+xml;utf8,' + encodeURIComponent(
  '<svg xmlns="http://www.w3.org/2000/svg" width="600" height="120"><rect width="600" height="120" fill="#2563eb"/></svg>'
);

const customer = (i: number) => ({
  id: `c${i}`, name: i === 0 ? LONG_NAME : `Tercero de prueba ${i}`, firstName: '', secondName: '', firstLastName: '', secondLastName: '',
  identificationType: '31', identificationNumber: `90012345${i}`, verificationDigit: '7', partyType: 'Cliente',
  email: i === 0 ? LONG_EMAIL : `tercero${i}@correo.com`, phone: '3001234567', address: 'Calle 1 # 2-3', cityCode: '11001', cityName: 'Bogotá',
  postalCode: '110111', taxRegime: '48', dataicoRegimen: 'ORDINARIO', dataicoTaxLevelCode: 'NO_RESPONSABLE_DE_IVA',
});

const item = (i: number) => ({
  id: `i${i}`, code: `SKU-${i}`, name: i === 0 ? LONG_NAME : `Servicio profesional ${i}`, quantity: 2, unitPrice: 150000.5, discountRate: 1,
  ivaTreatment: 'Gravado', taxRate: 19, taxAmount: 56000, totalAmount: 356000, productId: null, retentions: [],
  unitOfMeasureCode: '94', unitOfMeasureAbbreviation: 'EA', unitOfMeasureDisplayFormat: 'Combined',
});

const document = (i: number, typeCode: string) => ({
  id: `d${i}`, typeCode, number: String(500 + i), issueDate: '2026-10-01T00:00:00',
  status: ['DRAFT', 'APPROVED', 'REJECTED', 'PROCESSING'][i % 4], customer: customer(i), resolution: { id: 'r1', prefix: 'FEV', documentType: typeCode },
  subtotal: 1234567.89, taxAmount: 234567.89, totalAmount: 1469135.78, items: [item(0), item(1)], generalRetentions: [],
  relatedNotes: i === 0 ? [{ id: 'rn1', typeCode: 'NC', number: '99' }] : [],
  dianResponseMessage: i % 4 === 2 ? JSON.stringify({ errors: [{ path: ['support_doc'], error: LONG_TOKEN }] }) : null,
  paymentMeansType: 'DEBITO', purchaseOrderReference: LONG_TOKEN,
});

const product = (i: number) => ({
  id: `p${i}`, code: `SKU-${i}`, name: i === 0 ? LONG_NAME : `Producto ${i}`, unitPrice: 150000.123, ivaTreatment: 'Gravado', ivaRate: 19,
  taxes: [{ taxCategory: 'IMP_CONSUMO', rate: 8 }], unitOfMeasureId: '30000000-0000-0000-0000-000000000001', scope: 'Invoice',
});

const resolution = (i: number) => ({
  id: `r${i}`, resolutionNumber: '18760000001', prefix: i === 0 ? 'SETP' : 'FEV', numberStart: 1, numberEnd: 5000, nextNumber: 10,
  validFrom: '2026-01-01T00:00:00', validTo: '2027-01-01T00:00:00', documentType: i === 0 ? 'FE' : 'DS', isDefault: i === 0, branchIds: ['b1', 'b2'],
});

const payroll = (i: number) => ({
  id: `n${i}`, number: `NE${i}`, consecutiveNumber: `NE${i}`, status: ['DRAFT', 'APPROVED', 'REJECTED'][i % 3], issueDate: '2026-10-01T00:00:00',
  initialSettlementDate: '2026-09-01T00:00:00', finalSettlementDate: '2026-09-30T00:00:00', totalAmount: 2500000, typeCode: 'NE',
  customer: { name: i === 0 ? LONG_NAME : `Empleado ${i}`, identificationNumber: `1020304${i}` },
  dianResponseMessage: i % 3 === 2 ? LONG_TOKEN : null,
});

const received = (i: number) => ({
  id: `rd${i}`, issuerName: i === 0 ? LONG_NAME : `Proveedor ${i}`, issuerTaxId: '900111222', documentId: `FE-${LONG_TOKEN.slice(0, 20)}`,
  issueDate: '2026-10-01T00:00:00', totalAmount: 987654.32, sourceType: 'Email', events: [],
});

const branding = {
  companyName: LONG_NAME, logoLightUrl: '', logoDarkUrl: '', primaryColorLight: '#2563eb', primaryColorDark: '#f8fafc',
  hasCustomLogo: true, invoiceLogoUrl: WIDE_LOGO, clientName: LONG_NAME, taxId: '900123456', verificationDigit: '7', decimalSeparator: '.',
  unitOfMeasureDisplayOverride: '',
};

const summary = {
  period: { start: '2026-10-01', end: '2026-10-31' }, totalIssued: 120, totalAccepted: 100, totalPending: 10, totalRejected: 10, totalBilled: 98765432.1,
  recentDocuments: [0, 1, 2].map(i => ({ id: `d${i}`, typeCode: 'FV', number: String(500 + i), processedAt: '2026-10-01T12:00:00Z', customerName: i === 0 ? LONG_NAME : `Cliente ${i}`, totalAmount: 1469135.78 })),
  consumption: { mode: 'Standard', pricePerDocument: 450 }, pendingSetupItems: ['no-resolution'],
};

export const BRANCH_MAIN = { id: 'b1', name: 'Principal', code: 'PRINCIPAL', isMain: true };
export const BRANCH_NORTH = { id: 'b2', name: `Sucursal ${LONG_NAME}`, code: 'NORTE', isMain: false };

export type MockSession = {
  role: string; isAdministrator: boolean; allBranches: boolean;
  branches: Array<{ id: string; name: string; code: string; isMain: boolean }>;
  features?: { payments: boolean };
};

const sessionCatalogs = {
  roles: [{ value: 'Administrador', label: 'Administrador' }, { value: 'Facturador', label: 'Facturador' }],
  noteKinds: [{ value: 'CreditNote', label: 'Nota crédito' }, { value: 'DebitNote', label: 'Nota débito' }, { value: 'SupportAdjustment', label: 'Nota de ajuste' }],
};

// El Tenant activó el módulo de pagos para este cliente (apagado por defecto en la API).
export const ADMIN_SESSION: MockSession = { role: 'Administrador', isAdministrator: true, allBranches: true, branches: [BRANCH_MAIN, BRANCH_NORTH], features: { payments: true } };
export const INVOICER_SESSION: MockSession = { role: 'Facturador', isAdministrator: false, allBranches: false, branches: [BRANCH_NORTH] };

const users = [
  { id: 'u1', name: 'Administradora Principal', email: 'admin@empresa.com', role: 'Administrador', allBranches: true, branchIds: [], isActive: true, isSelf: true },
  { id: 'u2', name: LONG_NAME, email: LONG_EMAIL, role: 'Facturador', allBranches: false, branchIds: ['b1', 'b2'], isActive: true, isSelf: false },
  { id: 'u3', name: 'Usuario desactivado', email: 'inactivo@empresa.com', role: 'Facturador', allBranches: false, branchIds: ['b2'], isActive: false, isSelf: false },
];

const noteNumberings = [
  { branchId: 'b2', branchName: BRANCH_NORTH.name, kind: 'CreditNote', kindLabel: 'Nota crédito', prefix: 'NCN', nextNumber: 50 },
  { branchId: 'b2', branchName: BRANCH_NORTH.name, kind: 'SupportAdjustment', kindLabel: 'Nota de ajuste', prefix: 'NAJN', nextNumber: 1 },
];

const list = <T,>(n: number, make: (i: number) => T) => Array.from({ length: n }, (_, i) => make(i));

// Soporte aprobado con retenciones por ítem y generales, para probar que la nota de ajuste las copia.
const supportWithRetentions = {
  ...document(1, 'DS'), id: 'd1', status: 'APPROVED', number: '501', resolutionId: 'r1', notes: 'Observación del documento original',
  items: [{ ...item(0), retentions: [{ taxCategory: 'RET_FUENTE', rate: 11, baseAmount: 356000, amount: 39160 }] }],
  generalRetentions: [{ taxCategory: 'RET_ICA', rate: 0.414 }],
};

// Devuelve la respuesta simulada para una petición, o [] si el endpoint no está en la tabla.
function respond(pathname: string, search: string, session: MockSession): unknown {
  if (pathname.endsWith('/client/session')) return { features: { payments: false }, ...session, ...sessionCatalogs };
  if (pathname.endsWith('/client/users')) return users;
  if (pathname.endsWith('/client/note-numberings')) return noteNumberings;
  if (pathname.endsWith('/v1/branding/my-branding')) return branding;
  if (pathname.endsWith('/v1/dashboard/summary')) return summary;
  if (pathname.endsWith('/tenant/branding/demo')) return { commercialName: LONG_NAME, logoLightUrl: '', primaryColorLight: '#2563eb' };
  if (pathname.endsWith('/client/customers')) return list(4, customer);
  if (pathname.endsWith('/client/products')) return list(4, product);
  if (pathname.endsWith('/client/invoices')) return list(6, i => document(i, ['FE', 'FE', 'NC', 'ND', 'FE', 'FE'][i]));
  if (pathname.endsWith('/client/support-documents/d1')) return supportWithRetentions;
  if (pathname.endsWith('/client/support-documents')) return list(6, i => document(i, 'DS'));
  if (pathname.endsWith('/client/payroll')) return list(5, payroll);
  if (pathname.endsWith('/client/received-documents')) return list(4, received);
  if (pathname.endsWith('/client/resolutions')) return list(3, resolution);
  if (pathname.endsWith('/client/resolutions/note-counters')) return { nextCreditNoteNumber: 1, nextDebitNoteNumber: 1, nextSupportAdjustmentNumber: 1, supportAdjustmentPrefix: 'DSA' };
  if (pathname.endsWith('/client/dian/habilitation-status')) return { status: 'Production' };
  if (pathname.endsWith('/client/reception-settings')) return { receptionEmailEnabled: true, receptionEmailHost: 'imap.gmail.com', receptionEmailPort: 993, receptionEmailUseSsl: true, receptionEmailUser: LONG_EMAIL, hasPassword: true, autoSendAcuseRecibo: false, autoSendReciboBien: false, autoSendAceptacion: false, autoSendReclamo: false };
  if (pathname.endsWith('/client/smtp-settings')) return { smtpHost: 'smtp.gmail.com', smtpPort: 587, smtpUseSsl: true, smtpUser: LONG_EMAIL, smtpFromEmail: LONG_EMAIL, smtpFromName: LONG_NAME, hasPassword: true };
  if (pathname.endsWith('/client/auth/me')) return { name: 'Ana Prueba', displayName: 'Ana Prueba', email: 'ana@prueba.test', role: session.role, organization: 'Cliente de prueba', sessionMinutes: 30, allowedSessionMinutes: [15, 30, 60, 120, 240, 480], absoluteCapHours: 12 };
  if (pathname.endsWith('/client/me')) return { electronicInvoiceLegend: LONG_NAME, supportDocumentLegend: '' };
  if (pathname.endsWith('/client/templates/settings')) return [{ settingId: 's1', documentTypeId: 't1', documentTypeName: 'Factura Electrónica de Venta', selectedTemplateId: 'tp1', selectedTemplateName: LONG_NAME }];
  if (pathname.endsWith('/client/templates/available/t1')) return [{ id: 'tp1', name: LONG_NAME, isGlobal: true }, { id: 'tp2', name: 'Plantilla del proveedor', isGlobal: false }];
  if (pathname.endsWith('/client/templates')) return [{ id: 'm1', name: LONG_NAME, repxTemplateKey: `dgs/${LONG_TOKEN}`, status: 'Draft', versionNumber: 1, documentTypeId: 't1', documentType: 'FE', scope: 'Propio', mostrarRetenciones: true }];
  if (pathname.endsWith('/client/invoices/document-types')) return [{ id: 'dt1', code: '01', name: 'Factura electrónica de venta' }];
  if (pathname.endsWith('/client/identification-types')) return [{ code: '31', name: 'NIT' }, { code: '13', name: 'Cédula de ciudadanía' }];
  if (pathname.endsWith('/client/units-of-measure')) return [{ id: '30000000-0000-0000-0000-000000000001', dianCode: '94', abbreviation: 'EA', name: 'Unidad' }];
  if (pathname.endsWith('/client/tax-catalog')) {
    const kind = new URLSearchParams(search).get('kind');
    if (kind === 'IvaRate') return [{ id: 'iv1', category: '19', name: '19%' }, { id: 'iv2', category: '5', name: '5%' }];
    if (kind === 'FormaPago') return [{ id: 'fp1', category: 'DEBITO', name: 'Contado' }, { id: 'fp2', category: 'CREDITO', name: 'Crédito' }];
    if (kind === 'PaymentMeans') return [{ id: 'pm1', category: 'CASH', name: 'Efectivo' }];
    return [];
  }
  return [];
}

// Intercepta toda petición a /api y la responde con datos simulados. Debe llamarse antes de page.goto.
export async function mockApi(page: Page, session: MockSession = ADMIN_SESSION) {
  await page.route('**/api/**', route => {
    const url = new URL(route.request().url());
    const method = route.request().method();
    let body: any = method === 'GET' ? respond(url.pathname, url.search, session) : {};
    // Recepción: la principal tiene lo suyo; Norte (b2) hereda el buzón y los eventos de la principal.
    if (method === 'GET' && url.pathname.endsWith('/client/reception-settings') && body && !Array.isArray(body)) {
      const inheriting = route.request().headers()['x-branch-id'] === BRANCH_NORTH.id;
      body = { ...body, isMain: !inheriting, isBranchOwn: !inheriting, isEventsOwn: !inheriting };
    }
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
  });
}

// Sesión iniciada. El vigilante de sesión lee el vencimiento del token (sin validarlo), así que se arma uno con 12 horas de vida.
function fakeToken() {
  const enc = (o: object) => Buffer.from(JSON.stringify(o)).toString('base64url');
  const now = Math.floor(Date.now() / 1000);
  return `${enc({ alg: 'none' })}.${enc({ iat: now, exp: now + 43200, sst: now, cap: now + 86400 })}.x`;
}

export async function signIn(page: Page) {
  await page.addInitScript((token: string) => {
    localStorage.setItem('fel_client_auth', token);
    localStorage.setItem('fel_client_id', 'client-1');
    localStorage.setItem('fel_client_tenant', 'demo');
  }, fakeToken());
}
