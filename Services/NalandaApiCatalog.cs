using NalandaExplorer.Models;

namespace NalandaExplorer.Services;

public static class NalandaApiCatalog
{
    public const string AcceptV1 = "application/vnd.nalanda.api-v1.0+json";
    public const string AcceptHomologationsV1 = "application/vnd.nalanda.api-V1.0+json";
    public const string AcceptHomologationsV2 = "application/vnd.nalanda.api-V2.0+json";

    private static readonly NalandaApiParameter Page = new(
        "page", "Página", NalandaApiParameterLocation.Query,
        InputType: "number", Placeholder: "0", DefaultValue: "0");

    private static readonly NalandaApiParameter Size = new(
        "size", "Elementos por página", NalandaApiParameterLocation.Query,
        InputType: "number", Placeholder: "20", DefaultValue: "20");

    private static readonly NalandaApiParameter Sort = new(
        "sort", "Orden", NalandaApiParameterLocation.Query,
        Placeholder: "campo,asc");

    private static readonly NalandaApiParameter TaxId = new(
        "tax_id", "CIF / identificador fiscal", NalandaApiParameterLocation.Path,
        Required: true, Placeholder: "A12345678");

    private static readonly NalandaApiParameter ProjectId = new(
        "id", "Identificador de obra (ULID)", NalandaApiParameterLocation.Path,
        Required: true, Placeholder: "01H3KVBK79F0B8FYD2YV85BHVF");

    public static IReadOnlyList<NalandaApiOperation> Operations { get; } =
    [
        new("projects.list", "Obras", "Listar obras", "Busca las obras accesibles para la cuenta.", "/projects",
            [Page, Size, Sort], AcceptV1, SupportsJsonBody: true),
        new("projects.get", "Obras", "Consultar obra", "Obtiene una obra por su ULID.", "/projects/{id}",
            [ProjectId], AcceptV1),
        new("projects.payment-states", "Obras", "Estado del certificado de pago",
            "Consulta los estados del certificado de pago para una obra y periodo.", "/projects/{id}/payments_states",
            [
                ProjectId,
                new("work_date", "Mes de trabajo", NalandaApiParameterLocation.Query, Required: true,
                    InputType: "month", Description: "Formato AAAA-MM."),
                new("invoice_date", "Mes de facturación", NalandaApiParameterLocation.Query, Required: true,
                    InputType: "month", Description: "Formato AAAA-MM."),
                new("tax_id", "CIF de la contrata", NalandaApiParameterLocation.Query, Required: true)
            ], AcceptV1),
        new("projects.contacts", "Obras", "Contactos de obra", "Lista los contactos asociados a una obra.",
            "/projects/{id}/contacts", [ProjectId, Page, Size, Sort], AcceptV1),
        new("projects.external-refs", "Obras", "Referencia externa de obra",
            "Obtiene la referencia interna asociada a una obra.", "/projects/{id}/external_refs",
            [ProjectId], AcceptV1),

        new("contractors.list", "Contratas", "Listar contratas",
            "Busca relaciones de contrata accesibles para la cuenta.", "/contractors",
            [
                Page, Size, Sort,
                new("project_id", "Identificador de obra", NalandaApiParameterLocation.Query),
                new("company_tax_id", "CIF de la empresa", NalandaApiParameterLocation.Query),
                new("status", "Vigencia", NalandaApiParameterLocation.Query,
                    Options: ["all", "active", "inactive"])
            ], AcceptV1, SupportsJsonBody: true),
        new("contractors.get", "Contratas", "Consultar contrata",
            "Obtiene una contrata por su identificador.", "/contractors/{id}",
            [new("id", "ID Nalanda de contrata", NalandaApiParameterLocation.Path, Required: true,
                Placeholder: "01H3KVBK79F0B8FYD2YV85BHVF",
                Description: "Usa el campo id de «Listar contratas», no el código visible (p. ej. 001.25061).")],
            AcceptV1),

        new("companies.subcontractors", "Empresas", "Subcontratas de una empresa",
            "Lista empresas contratadas por la empresa indicada.", "/companies/{tax_id}/subcontractors",
            [TaxId, Page, Size, Sort,
                new("status", "Vigencia", NalandaApiParameterLocation.Query,
                    Options: ["all", "active", "inactive"])], AcceptV1),
        new("companies.zones", "Empresas", "Zonas de una empresa",
            "Lista las zonas asociadas a una empresa.", "/companies/{tax_id}/zones",
            [TaxId, Page, Size, Sort], AcceptV1),

        new("contractor-workers.search", "Trabajadores", "Trabajadores de contrata",
            "Busca trabajadores por contrata o documento de identidad.", "/contractors_workers",
            [
                Page, Size, Sort,
                new("contractor_id", "Identificador de contrata", NalandaApiParameterLocation.Query),
                new("worker_id_card", "Documento del trabajador", NalandaApiParameterLocation.Query)
            ], AcceptV1, AtLeastOneRequired: ["contractor_id", "worker_id_card"]),
        new("workers.company-clockings", "Fichajes", "Fichajes por empresa",
            "Consulta los fichajes de una empresa para una fecha.", "/companies/{tax_id}/clockings",
            [TaxId, ClockingDate(), Page, Size, Sort], AcceptV1),
        new("workers.project-clockings", "Fichajes", "Fichajes por obra",
            "Consulta los fichajes vinculados a una obra para una fecha.", "/projects/{id}/clockings",
            [ProjectId, ClockingDate(), Page, Size, Sort], AcceptV1),

        new("homologations.search", "Homologaciones", "Buscar homologaciones",
            "Busca homologaciones por CIF de homologador o proveedor.", "/homologations",
            [
                new("homologator_tax_id", "CIF del homologador", NalandaApiParameterLocation.Query),
                new("provider_tax_id", "CIF del proveedor", NalandaApiParameterLocation.Query),
                new("sort", "Orden", NalandaApiParameterLocation.Query, DefaultValue: "business_name,asc"),
                new("size", "Elementos por página", NalandaApiParameterLocation.Query,
                    InputType: "number", DefaultValue: "10"),
                Page
            ], AcceptHomologationsV2),
        new("homologations.get", "Homologaciones", "Consultar homologación",
            "Obtiene los datos principales de una homologación.", "/homologations/{id}",
            [HomologationId()], AcceptHomologationsV1),
        new("homologations.ratings", "Homologaciones", "Valoraciones de homologación",
            "Consulta la valoración total y las valoraciones por categoría.", "/homologations/{id}/ratings",
            [HomologationId()], AcceptHomologationsV1),
        new("homologations.provider", "Homologaciones", "Proveedor de homologación",
            "Obtiene la información básica del proveedor.", "/homologations/{id}/providers",
            [HomologationId()], AcceptHomologationsV1),
        new("homologations.dragados-answers", "Homologaciones", "Respuestas Dragados",
            "Obtiene las respuestas de una homologación Dragados (plataforma nacional).",
            "/homologations/{id}/dragados/answers", [HomologationId()], AcceptHomologationsV1),

        new("workers.by-company", "Trabajadores", "Trabajadores por empresa",
            "Busca trabajadores pertenecientes a una empresa.", "/companies/{tax_id}/workers",
            [
                TaxId, Page, Size, Sort,
                new("full_name", "Nombre y apellidos", NalandaApiParameterLocation.Query,
                    Placeholder: "ilike:nombre"),
                new("id_card", "Documento del trabajador", NalandaApiParameterLocation.Query)
            ], AcceptV1),
        new("workers.get", "Trabajadores", "Consultar trabajador",
            "Obtiene un trabajador por empresa y documento.", "/companies/{tax_id}/workers/{id_card}",
            [
                TaxId,
                new("id_card", "Documento del trabajador", NalandaApiParameterLocation.Path, Required: true)
            ], AcceptV1),
        new("workers.search", "Trabajadores", "Buscar trabajadores",
            "Busca trabajadores de la empresa de la cuenta autenticada.", "/workers",
            [Page, Size, Sort], AcceptV1)
    ];

    private static NalandaApiParameter ClockingDate() => new(
        "date_at", "Fecha del fichaje", NalandaApiParameterLocation.Query,
        Required: true, InputType: "date");

    private static NalandaApiParameter HomologationId() => new(
        "id", "Identificador de homologación", NalandaApiParameterLocation.Path,
        Required: true, Placeholder: "01HG8Q4AM7KZ5ZHWMJKCYKQQHX");
}
