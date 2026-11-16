using System.Xml;
using System.Xml.Linq;
using NalandaExplorer.Models;

namespace NalandaExplorer.Services;

internal static class NalandaSoapResponseParser
{
    public static (
        bool IsXml,
        string? FaultMessage,
        NalandaSoapProject? Project) Parse(string body, long maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (false, null, null);
        }

        if (maximumCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        }

        try
        {
            using var reader = XmlReader.Create(
                new StringReader(body),
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = maximumCharacters
                });
            var document = XDocument.Load(reader);
            var root = document.Root;
            var fault = FindElement(root, "Fault");
            var faultMessage = fault is null ? null : GetFaultMessage(fault);
            var projectElement = FindElement(root, "informacionExportacionObra");
            var project = projectElement is null ? null : ParseProject(projectElement);

            return (true, faultMessage, project);
        }
        catch (XmlException)
        {
            return (false, null, null);
        }
    }

    private static string? GetFaultMessage(XElement fault) =>
        GetChildValue(fault, "faultstring") ??
        FindElement(fault, "Text")?.Value.Trim();

    private static NalandaSoapProject ParseProject(XElement project)
    {
        var contractors = project.Descendants()
            .Where(element =>
                element.Name.LocalName == "contrata" &&
                element.Parent?.Name.LocalName == "contratas")
            .Select(ParseContractor)
            .ToArray();

        return new NalandaSoapProject(
            GetAttribute(project, "codigo"),
            GetAttribute(project, "cif"),
            contractors);
    }

    private static NalandaSoapContractor ParseContractor(XElement contractor)
    {
        var company = FindElement(contractor, "empresa");
        return new NalandaSoapContractor(
            GetAttribute(contractor, "id"),
            GetAttribute(contractor, "cif"),
            GetAttribute(contractor, "idContrataPadre"),
            company is null ? null : GetChildValue(company, "nombre"),
            GetChildValue(contractor, "fechaEntrada"),
            GetChildValue(contractor, "fechaSalida"),
            ParseWorkers(contractor),
            ParseMachinery(contractor),
            ParseDocuments(contractor));
    }

    private static IReadOnlyList<NalandaSoapWorker> ParseWorkers(XElement contractor) =>
        contractor.Descendants()
            .Where(element =>
                element.Name.LocalName == "trabajador" &&
                element.Parent?.Name.LocalName == "trabajadores")
            .Select(worker => new NalandaSoapWorker(
                string.Join(
                    " ",
                    new[] { GetChildValue(worker, "nombre"), GetChildValue(worker, "apellidos") }
                        .Where(value => !string.IsNullOrWhiteSpace(value))),
                GetAttribute(worker, "cif"),
                GetChildValue(worker, "fechaEntrada"),
                GetChildValue(worker, "fechaSalida"),
                worker.Descendants().Count(element => element.Name.LocalName == "documento")))
            .ToArray();

    private static IReadOnlyList<NalandaSoapMachinery> ParseMachinery(XElement contractor) =>
        contractor.Descendants()
            .Where(element =>
                element.Name.LocalName == "maquinaria" &&
                element.Parent?.Name.LocalName == "maquinarias")
            .Select(machine => new NalandaSoapMachinery(
                GetAttribute(machine, "matricula"),
                machine.Descendants().Count(element => element.Name.LocalName == "documento")))
            .ToArray();

    private static IReadOnlyList<NalandaSoapDocument> ParseDocuments(XElement contractor) =>
        contractor.Descendants()
            .Where(element => element.Name.LocalName == "documento")
            .Select(document => new NalandaSoapDocument(
                GetChildValue(document, "url"),
                GetAttribute(document, "tipo"),
                GetAttribute(document, "estado"),
                GetAttribute(document, "descripcion"),
                GetChildValue(document, "fechaCarga"),
                GetChildValue(document, "fechaEmision"),
                GetChildValue(document, "fechaVencimiento")))
            .ToArray();

    private static XElement? FindElement(XElement? parent, string name)
    {
        if (parent?.Name.LocalName == name)
        {
            return parent;
        }

        return parent?.Descendants().FirstOrDefault(element => element.Name.LocalName == name);
    }

    private static string? GetChildValue(XElement parent, string name) =>
        parent.Elements()
            .FirstOrDefault(element => element.Name.LocalName == name)?
            .Value.Trim();

    private static string? GetAttribute(XElement element, string name) =>
        element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)?
            .Value.Trim();
}
