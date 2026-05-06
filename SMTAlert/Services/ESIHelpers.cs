using EVEStandard.Models.API;

namespace SMT.EVEData
{
    public class ESIHelpers
    {
        public static bool ValidateESICall<T>(ESIModelDTO<T> esiR)
        {
            return esiR != null && esiR.Model != null;
        }
    }
}
