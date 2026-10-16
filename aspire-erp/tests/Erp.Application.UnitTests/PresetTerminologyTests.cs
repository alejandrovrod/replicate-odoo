using Erp.Application.Services;
using Erp.Application.UnitTests.Fakes;
using Xunit;

namespace Erp.Application.UnitTests;

/// <summary>
/// Chameleon architecture coverage (workflow Fase 5, gap D4): every bundled industry preset
/// must ship the FULL terminology override map in BOTH languages, keyed by the canonical
/// frontend catalog keys (namespace -> dotted key). A missing key means a screen keeps the
/// base vocabulary while its siblings speak the vertical - the exact discrepancy this guards.
/// <see cref="VerticalPresetService.Load"/> parses the embedded JSON, so this also pins the
/// files as valid documents.
/// </summary>
public sealed class PresetTerminologyTests
{
    private readonly VerticalPresetService _service = new(
        new FakeCustomFieldDefinitionRepository(),
        new FakeWorkflowRepository(),
        new FakeTaxTemplateRepository(),
        new FakeAccountRepository(),
        new FakeTenantPresetRepository());

    private string Term(string preset, string language, string ns, string key)
    {
        var document = _service.Load(preset);
        Assert.True(
            document.Terminology.TryGetValue(language, out var namespaces),
            $"{preset}: missing '{language}' terminology block.");
        Assert.True(
            namespaces.TryGetValue(ns, out var terms),
            $"{preset}/{language}: missing '{ns}' namespace.");
        Assert.True(
            terms.TryGetValue(key, out var value),
            $"{preset}/{language}/{ns}: missing key '{key}'.");
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }

    // ------------------------------------------------------------------ healthcare

    [Theory]
    [InlineData("en", "Patients")]
    [InlineData("es", "Pacientes")]
    public void Healthcare_CustomersTitle_CoversBothLanguages(string language, string expected) =>
        Assert.Equal(expected, Term("healthcare", language, "selling", "customers.manageTitle"));

    [Theory]
    [InlineData("en", "Patient Name", "Patient Type")]
    [InlineData("es", "Nombre del Paciente", "Tipo de Paciente")]
    public void Healthcare_CustomerColumns_CoverBothLanguages(
        string language, string name, string type)
    {
        Assert.Equal(name, Term("healthcare", language, "selling", "customers.columns.name"));
        Assert.Equal(type, Term("healthcare", language, "selling", "customers.columns.type"));
    }

    [Theory]
    [InlineData("en", "Admission Orders")]
    [InlineData("es", "Órdenes de Admisión")]
    public void Healthcare_OrdersTitle_CoversBothLanguages(string language, string expected)
    {
        Assert.Equal(expected, Term("healthcare", language, "selling", "orders.title"));
        Assert.Equal(expected, Term("healthcare", language, "selling", "overview.orders"));
    }

    [Theory]
    [InlineData("en", "selling", "orderForm.titleNew", "New Admission Order")]
    [InlineData("es", "selling", "orderForm.titleNew", "Nueva orden de admisión")]
    [InlineData("en", "selling", "form.customer", "Patient *")]
    [InlineData("es", "selling", "form.customer", "Paciente *")]
    [InlineData("en", "stock", "items.manageTitle", "Medicines & Supplies")]
    [InlineData("es", "stock", "items.manageTitle", "Medicamentos e Insumos")]
    [InlineData("en", "stock", "itemForm.titleNew", "New Medicine / Supply")]
    [InlineData("es", "stock", "itemForm.titleNew", "Nuevo Medicamento / Insumo")]
    [InlineData("en", "common", "nav.item.sellingCustomers", "Patients")]
    [InlineData("es", "common", "nav.item.sellingCustomers", "Pacientes")]
    [InlineData("en", "common", "nav.item.sellingOrders", "Admission Orders")]
    [InlineData("es", "common", "nav.item.sellingOrders", "Órdenes de Admisión")]
    [InlineData("en", "common", "nav.item.stock", "Medicines & Supplies")]
    [InlineData("es", "common", "nav.item.stock", "Medicamentos e Insumos")]
    public void Healthcare_ScreenVocabulary_CoversBothLanguages(
        string language, string ns, string key, string expected) =>
        Assert.Equal(expected, Term("healthcare", language, ns, key));

    // ------------------------------------------------------------------ automotive

    [Theory]
    [InlineData("en", "selling", "customers.manageTitle", "Vehicle Owners")]
    [InlineData("es", "selling", "customers.manageTitle", "Propietarios")]
    [InlineData("en", "selling", "customers.columns.name", "Owner Name")]
    [InlineData("es", "selling", "customers.columns.name", "Nombre del Propietario")]
    [InlineData("en", "selling", "form.customer", "Vehicle Owner *")]
    [InlineData("es", "selling", "form.customer", "Propietario *")]
    [InlineData("en", "selling", "orders.title", "Service Orders")]
    [InlineData("es", "selling", "orders.title", "Órdenes de Servicio")]
    [InlineData("en", "selling", "overview.orders", "Service Orders")]
    [InlineData("es", "selling", "overview.orders", "Órdenes de Servicio")]
    [InlineData("en", "selling", "orderForm.titleNew", "New Service Order")]
    [InlineData("es", "selling", "orderForm.titleNew", "Nueva Orden de Servicio")]
    [InlineData("en", "stock", "items.manageTitle", "Spare Parts & Components")]
    [InlineData("es", "stock", "items.manageTitle", "Repuestos y Refacciones")]
    [InlineData("en", "stock", "itemForm.titleNew", "New Spare Part")]
    [InlineData("es", "stock", "itemForm.titleNew", "Nuevo Repuesto")]
    [InlineData("en", "stock", "itemForm.titleEdit", "Edit Spare Part")]
    [InlineData("es", "stock", "itemForm.titleEdit", "Editar Repuesto")]
    [InlineData("en", "assets", "assets.manageTitle", "Vehicles & Equipment")]
    [InlineData("es", "assets", "assets.manageTitle", "Vehículos y Equipos")]
    [InlineData("en", "assets", "assets.titleNew", "New Vehicle / Equipment")]
    [InlineData("es", "assets", "assets.titleNew", "Nuevo Vehículo / Equipo")]
    [InlineData("en", "common", "nav.item.sellingOrders", "Service Orders")]
    [InlineData("es", "common", "nav.item.sellingOrders", "Órdenes de Servicio")]
    [InlineData("en", "common", "nav.item.stock", "Spare Parts")]
    [InlineData("es", "common", "nav.item.stock", "Repuestos")]
    [InlineData("en", "common", "nav.item.assets", "Vehicles")]
    [InlineData("es", "common", "nav.item.assets", "Vehículos")]
    public void Automotive_ScreenVocabulary_CoversBothLanguages(
        string language, string ns, string key, string expected) =>
        Assert.Equal(expected, Term("automotive", language, ns, key));

    // ----------------------------------------------------------------- construction

    [Theory]
    [InlineData("en", "buying", "suppliers.manageTitle", "Subcontractors")]
    [InlineData("es", "buying", "suppliers.manageTitle", "Subcontratistas")]
    [InlineData("en", "buying", "suppliers.columns.name", "Subcontractor Name")]
    [InlineData("es", "buying", "suppliers.columns.name", "Nombre del Subcontratista")]
    [InlineData("en", "buying", "supplierForm.titleNew", "New Subcontractor")]
    [InlineData("es", "buying", "supplierForm.titleNew", "Nuevo Subcontratista")]
    [InlineData("en", "selling", "orders.title", "Work Orders")]
    [InlineData("es", "selling", "orders.title", "Órdenes de Obra")]
    [InlineData("en", "selling", "orderForm.titleNew", "New Work Order")]
    [InlineData("es", "selling", "orderForm.titleNew", "Nueva Orden de Obra")]
    [InlineData("en", "stock", "items.manageTitle", "Jobsite Materials")]
    [InlineData("es", "stock", "items.manageTitle", "Materiales de Obra")]
    [InlineData("en", "stock", "itemForm.titleNew", "New Jobsite Material")]
    [InlineData("es", "stock", "itemForm.titleNew", "Nuevo Material de Obra")]
    [InlineData("en", "assets", "assets.manageTitle", "Machinery & Equipment")]
    [InlineData("es", "assets", "assets.manageTitle", "Maquinaria y Equipos")]
    [InlineData("en", "common", "nav.item.buyingSuppliers", "Subcontractors")]
    [InlineData("es", "common", "nav.item.buyingSuppliers", "Subcontratistas")]
    [InlineData("en", "common", "nav.item.sellingOrders", "Work Orders")]
    [InlineData("es", "common", "nav.item.sellingOrders", "Órdenes de Obra")]
    [InlineData("en", "common", "nav.item.stock", "Materials")]
    [InlineData("es", "common", "nav.item.stock", "Materiales")]
    public void Construction_ScreenVocabulary_CoversBothLanguages(
        string language, string ns, string key, string expected) =>
        Assert.Equal(expected, Term("construction", language, ns, key));

    // ----------------------------------------------------------------- distribution

    [Theory]
    [InlineData("en", "selling", "customers.manageTitle", "Outlets / Customers")]
    [InlineData("es", "selling", "customers.manageTitle", "Puntos de Venta / Clientes")]
    [InlineData("en", "selling", "form.customer", "Outlet / Customer *")]
    [InlineData("es", "selling", "form.customer", "Punto de Venta / Cliente *")]
    [InlineData("en", "selling", "orders.title", "Fulfillment Orders")]
    [InlineData("es", "selling", "orders.title", "Pedidos de Surtido")]
    [InlineData("en", "selling", "orderForm.titleNew", "New Fulfillment Order")]
    [InlineData("es", "selling", "orderForm.titleNew", "Nuevo Pedido de Surtido")]
    [InlineData("en", "selling", "deliveries.title", "Dispatch Guides")]
    [InlineData("es", "selling", "deliveries.title", "Guías de Despacho")]
    [InlineData("en", "selling", "deliveryForm.titleNew", "New Dispatch Guide")]
    [InlineData("es", "selling", "deliveryForm.titleNew", "Nueva Guía de Despacho")]
    [InlineData("en", "stock", "items.manageTitle", "Products & SKUs")]
    [InlineData("es", "stock", "items.manageTitle", "Productos y SKUs")]
    [InlineData("en", "stock", "itemForm.titleNew", "New Product / SKU")]
    [InlineData("es", "stock", "itemForm.titleNew", "Nuevo Producto / SKU")]
    [InlineData("en", "common", "nav.item.sellingOrders", "Fulfillment Orders")]
    [InlineData("es", "common", "nav.item.sellingOrders", "Pedidos de Surtido")]
    [InlineData("en", "common", "nav.item.sellingDeliveries", "Dispatch Guides")]
    [InlineData("es", "common", "nav.item.sellingDeliveries", "Guías de Despacho")]
    public void Distribution_ScreenVocabulary_CoversBothLanguages(
        string language, string ns, string key, string expected) =>
        Assert.Equal(expected, Term("distribution", language, ns, key));
}
