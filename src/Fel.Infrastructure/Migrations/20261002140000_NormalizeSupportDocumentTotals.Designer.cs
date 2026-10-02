using Fel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fel.Infrastructure.Migrations
{
    [DbContext(typeof(FelDbContext))]
    [Migration("20261002140000_NormalizeSupportDocumentTotals")]
    partial class NormalizeSupportDocumentTotals
    {
    }
}
