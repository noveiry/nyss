using System.Collections.Generic;
using RX.Nyss.Web.Services.EidsrClient.Dto;

namespace RX.Nyss.Common.Services.DhisClient.Dto;
public class DhisRegisterReportRequest
{
    public DhisRegisterReportRequestBody DhisRegisterReportRequestBody { get; set; }

    public EidsrApiProperties EidsrApiProperties { get; set; }

    private DhisRegisterReportRequest()
    {
    }

    public static DhisRegisterReportRequest CreateDhisRegisterReportRequest(
        DhisRegisterReportRequestTemplate template,
        DhisRegisterReportRequestData data)
    {
        var res = new DhisRegisterReportRequest
        {
            EidsrApiProperties = template.EidsrApiProperties,
            DhisRegisterReportRequestBody = new DhisRegisterReportRequestBody
            {
                Program = template.Program,
                EventDate = data.EventDate,
                OrgUnit = data.OrgUnit,
                DataValues = GetDataValues(template, data)
            }
        };

        return res;
    }

    private static List<DhisRegisterReportRequestBody.DataValue> GetDataValues(
        DhisRegisterReportRequestTemplate template,
        DhisRegisterReportRequestData data)
    {
        var dataValues = new List<DhisRegisterReportRequestBody.DataValue>();

        if (template.ReportLocationDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportLocationDataElementId, data.ReportLocation);
        }
        if (template.ReportGeoLocationDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportGeoLocationDataElementId, data.ReportGeoLocation.ToString());
        }
        if (template.ReportSuspectedDiseaseDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportSuspectedDiseaseDataElementId, data.ReportSuspectedDisease);
        }
        if (template.ReportHealthRiskDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportHealthRiskDataElementId, data.ReportHealthRisk);
        }
        if (template.ReportStatusDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportStatusDataElementId, data.ReportStatus);
        }
        if (template.ReportGenderDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportGenderDataElementId, data.ReportGender);
        }
        if (template.ReportAgeGroupDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportAgeGroupDataElementId, data.ReportAgeGroup);
        }
        if (template.ReportCaseCountFemaleAgeAtLeastFiveDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportCaseCountFemaleAgeAtLeastFiveDataElementId, data.ReportCaseCountFemaleAgeAtLeastFive.ToString());
        }
        if (template.ReportCaseCountMaleAgeAtLeastFiveDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportCaseCountMaleAgeAtLeastFiveDataElementId, data.ReportCaseCountMaleAgeAtLeastFive.ToString());
        }
        if (template.ReportCaseCountFemaleAgeBelowFiveDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportCaseCountFemaleAgeBelowFiveDataElementId, data.ReportCaseCountFemaleAgeBelowFive.ToString());
        }
        if (template.ReportCaseCountMaleAgeBelowFiveDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportCaseCountMaleAgeBelowFiveDataElementId, data.ReportCaseCountMaleAgeBelowFive.ToString());
        }
        if (template.ReportDateDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportDateDataElementId, data.ReportDate);
        }
        if (template.ReportTimeDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportTimeDataElementId, data.ReportTime);
        }
        if (template.ReportDataCollectorIdDataElementId != "")
        {
            AddDataElement(dataValues, template.ReportDataCollectorIdDataElementId, data.ReportDataCollectorId.ToString());
        }

        return dataValues;
    }

    private static void AddDataElement(List<DhisRegisterReportRequestBody.DataValue> list, string dataElementId, string value)
    {
        if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(dataElementId))
        {
            list.Add(new DhisRegisterReportRequestBody.DataValue
            {
                DataElement = dataElementId,
                Value = value,
            });
        }
    }
}
