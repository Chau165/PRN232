using PRN232.Application.ReadModels;

namespace PRN232.Application.Interfaces;

public interface IAdministrativeDivisionCatalog
{
    IReadOnlyList<AdministrativeDivisionReadModel> GetProvinces();

    IReadOnlyList<AdministrativeDivisionReadModel> GetCommunes(string provinceCode);

    AdministrativeDivisionReadModel? GetProvince(string provinceCode);

    AdministrativeDivisionReadModel? GetCommune(string communeCode);
}
