const fs = require('fs');
const path = require('path');

const localesDir = path.join(__dirname, 'src/frontend/erp-client/public/locales');

const updates = {
  'stock.json': {
    en: {
      items: {
        manageTitle: "Items & Products",
        manageSubtitle: "Manage SKUs, products, and services.",
        search: "Search items...",
        columns: { code: "Code", name: "Name", uom: "Base UOM", valuation: "Valuation", status: "Status" },
        active: "Active",
        inactive: "Disabled",
        manage: "Manage Items"
      },
      itemForm: {
        titleNew: "New Item",
        titleEdit: "Edit Item",
        codeLabel: "Item Code *",
        nameLabel: "Item Name *",
        uomLabel: "Base UOM",
        valuationLabel: "Valuation Method",
        active: "Active",
        cancel: "Cancel",
        save: "Save",
        saving: "Saving...",
        errorRequired: "Name and Code are required.",
        errorConflict: "Concurrency conflict or duplicate code. Please refresh and try again.",
        errorGeneric: "An error occurred while saving."
      }
    },
    es: {
      items: {
        manageTitle: "Artículos y Productos",
        manageSubtitle: "Gestionar SKUs, productos y servicios.",
        search: "Buscar artículos...",
        columns: { code: "Código", name: "Nombre", uom: "UdM Base", valuation: "Valoración", status: "Estado" },
        active: "Activo",
        inactive: "Inactivo",
        manage: "Administrar Artículos"
      },
      itemForm: {
        titleNew: "Nuevo Artículo",
        titleEdit: "Editar Artículo",
        codeLabel: "Código de Artículo *",
        nameLabel: "Nombre de Artículo *",
        uomLabel: "UdM Base",
        valuationLabel: "Método de Valoración",
        active: "Activo",
        cancel: "Cancelar",
        save: "Guardar",
        saving: "Guardando...",
        errorRequired: "El nombre y el código son obligatorios.",
        errorConflict: "Conflicto de concurrencia o código duplicado. Refresque e intente de nuevo.",
        errorGeneric: "Ocurrió un error al guardar."
      }
    }
  },
  'selling.json': {
    en: {
      customers: {
        manageTitle: "Customers",
        manageSubtitle: "Manage your customers and credit limits.",
        search: "Search customers...",
        columns: { code: "Code", name: "Name", taxId: "Tax ID", creditLimit: "Credit Limit", status: "Status" },
        active: "Active",
        inactive: "Disabled"
      },
      customerForm: {
        titleNew: "New Customer",
        titleEdit: "Edit Customer",
        codeLabel: "Customer Code *",
        nameLabel: "Customer Name *",
        taxIdLabel: "Tax ID",
        creditLimitLabel: "Credit Limit",
        active: "Active",
        cancel: "Cancel",
        save: "Save",
        saving: "Saving...",
        errorRequired: "Name and Code are required.",
        errorConflict: "Concurrency conflict or duplicate code. Please refresh and try again.",
        errorGeneric: "An error occurred while saving."
      },
      overview: { manageCustomers: "Manage Customers" }
    },
    es: {
      customers: {
        manageTitle: "Clientes",
        manageSubtitle: "Gestionar clientes y límites de crédito.",
        search: "Buscar clientes...",
        columns: { code: "Código", name: "Nombre", taxId: "CUIT/RUT", creditLimit: "Límite Crédito", status: "Estado" },
        active: "Activo",
        inactive: "Inactivo"
      },
      customerForm: {
        titleNew: "Nuevo Cliente",
        titleEdit: "Editar Cliente",
        codeLabel: "Código Cliente *",
        nameLabel: "Nombre Cliente *",
        taxIdLabel: "CUIT/RUT",
        creditLimitLabel: "Límite de Crédito",
        active: "Activo",
        cancel: "Cancelar",
        save: "Guardar",
        saving: "Guardando...",
        errorRequired: "El nombre y el código son obligatorios.",
        errorConflict: "Conflicto de concurrencia o código duplicado. Refresque e intente de nuevo.",
        errorGeneric: "Ocurrió un error al guardar."
      },
      overview: { manageCustomers: "Clientes" }
    }
  },
  'buying.json': {
    en: {
      suppliers: {
        manageTitle: "Suppliers",
        manageSubtitle: "Manage your suppliers and payment terms.",
        search: "Search suppliers...",
        columns: { code: "Code", name: "Name", taxId: "Tax ID", status: "Status" },
        active: "Active",
        inactive: "Disabled"
      },
      supplierForm: {
        titleNew: "New Supplier",
        titleEdit: "Edit Supplier",
        codeLabel: "Supplier Code *",
        nameLabel: "Supplier Name *",
        taxIdLabel: "Tax ID",
        active: "Active",
        cancel: "Cancel",
        save: "Save",
        saving: "Saving...",
        errorRequired: "Name and Code are required.",
        errorConflict: "Concurrency conflict or duplicate code. Please refresh and try again.",
        errorGeneric: "An error occurred while saving."
      },
      overview: { manageSuppliers: "Manage Suppliers" }
    },
    es: {
      suppliers: {
        manageTitle: "Proveedores",
        manageSubtitle: "Gestionar proveedores y condiciones de pago.",
        search: "Buscar proveedores...",
        columns: { code: "Código", name: "Nombre", taxId: "CUIT/RUT", status: "Estado" },
        active: "Activo",
        inactive: "Inactivo"
      },
      supplierForm: {
        titleNew: "Nuevo Proveedor",
        titleEdit: "Editar Proveedor",
        codeLabel: "Código Proveedor *",
        nameLabel: "Nombre Proveedor *",
        taxIdLabel: "CUIT/RUT",
        active: "Activo",
        cancel: "Cancelar",
        save: "Guardar",
        saving: "Guardando...",
        errorRequired: "El nombre y el código son obligatorios.",
        errorConflict: "Conflicto de concurrencia o código duplicado. Refresque e intente de nuevo.",
        errorGeneric: "Ocurrió un error al guardar."
      },
      overview: { manageSuppliers: "Proveedores" }
    }
  },
  'accounting.json': {
    en: {
      accountForm: {
        titleNew: "New Account",
        titleEdit: "Edit Account",
        codeLabel: "Account Code *",
        nameLabel: "Account Name *",
        rootTypeLabel: "Root Type",
        currencyLabel: "Currency",
        parentLabel: "Parent Group Account",
        accountNone: "-- No Parent --",
        isGroup: "Is Group Account? (Cannot contain entries)",
        active: "Active",
        cancel: "Cancel",
        save: "Save",
        saving: "Saving...",
        errorRequired: "Code and Name are required.",
        errorConflict: "Concurrency conflict or duplicate code. Please try again.",
        errorGeneric: "An error occurred while saving."
      },
      tree: {
        newAccount: "New Account",
        actionsColumn: "",
        edit: "Edit Account"
      }
    },
    es: {
      accountForm: {
        titleNew: "Nueva Cuenta",
        titleEdit: "Editar Cuenta",
        codeLabel: "Código de Cuenta *",
        nameLabel: "Nombre de Cuenta *",
        rootTypeLabel: "Tipo Raíz",
        currencyLabel: "Moneda",
        parentLabel: "Cuenta Grupo Padre",
        accountNone: "-- Sin Padre --",
        isGroup: "¿Es Cuenta Grupo? (No recibe asientos)",
        active: "Activo",
        cancel: "Cancelar",
        save: "Guardar",
        saving: "Guardando...",
        errorRequired: "El código y el nombre son obligatorios.",
        errorConflict: "Conflicto de concurrencia o código duplicado. Intente nuevamente.",
        errorGeneric: "Ocurrió un error al guardar."
      },
      tree: {
        newAccount: "Nueva Cuenta",
        actionsColumn: "",
        edit: "Editar Cuenta"
      }
    }
  }
};

for (const [file, langs] of Object.entries(updates)) {
  for (const [lang, data] of Object.entries(langs)) {
    const filePath = path.join(localesDir, lang, file);
    if (fs.existsSync(filePath)) {
      const content = JSON.parse(fs.readFileSync(filePath, 'utf8'));
      
      // Deep merge
      for (const [key, val] of Object.entries(data)) {
        if (!content[key]) {
          content[key] = val;
        } else if (typeof val === 'object' && !Array.isArray(val)) {
          Object.assign(content[key], val);
        }
      }
      
      fs.writeFileSync(filePath, JSON.stringify(content, null, 2));
      console.log(`Updated ${lang}/${file}`);
    }
  }
}
