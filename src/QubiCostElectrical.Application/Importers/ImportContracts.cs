namespace QubiCostElectrical;

public interface IProjectImporter
{
    ProjectFileData Import(string path);
}
