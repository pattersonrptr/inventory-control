// Global usings added during the Fase 5 modular monolith restructure.
// These keep all existing code compiling without updating every individual file.
// Explicit per-file usings will be cleaned up in sub-phase 5.9.

// Domain
global using InventoryControl.Domain.Audit;
global using InventoryControl.Domain.Catalog;
global using InventoryControl.Domain.Identity;
global using InventoryControl.Domain.Integrations;
global using InventoryControl.Domain.Orders;
global using InventoryControl.Domain.Products;
global using InventoryControl.Domain.Shared;
global using InventoryControl.Domain.Stock;

// Infrastructure — old namespace aliases for backward compatibility
global using InventoryControl.Infrastructure.Persistence;
global using InventoryControl.Infrastructure.Persistence.Repositories;
