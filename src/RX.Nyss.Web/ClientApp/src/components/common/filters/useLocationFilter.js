import { useEffect, useState } from "react";
import { renderFilterLabel } from "../../common/filters/logic/locationFilterService";
import { strings, stringKeys } from "../../../strings";

const useLocationFilter = (locations, localFilters, updateLocalFilters, showUnknownLocation) => {
  const [locationsFilterLabel, setLocationsFilterLabel] = useState(
    strings(stringKeys.filters.area.all),
  );

  // Synchronize locations from redux store with filter state
  // useEffect which runs on mount and when locations are added, edited or removed. Updates locations in the filter state in order to avoid mismatch between locations and filtered locations
  useEffect(() => {
    if (!locations) return;

    const locationFilters = {
      regionIds: locations.regions.map((region) => region.id),
      districtIds: locations.regions
        .map((region) => region.districts.map((district) => district.id))
        .flat(),
      villageIds: locations.regions
        .map((region) =>
          region.districts.map((district) =>
            district.villages.map((village) => village.id),
          ),
        )
        .flat(2),
      zoneIds: locations.regions
        .map((region) =>
          region.districts.map((district) =>
            district.villages.map((village) =>
              village.zones.map((zone) => zone.id),
            ),
          ),
        )
        .flat(3),
      includeUnknownLocation: showUnknownLocation ? true : false,
    };

    updateLocalFilters({ locations: locationFilters });
  }, [locations]);

  // Sets label for location filter to 'All' or "Region (+n)"
  useEffect(() => {
    const label =
      !localFilters ||
      !locations ||
      !localFilters.locations ||
      localFilters.locations.regionIds.length === 0 && !showUnknownLocation
        ? strings(stringKeys.filters.area.all)
        : renderFilterLabel(localFilters.locations, locations.regions, showUnknownLocation);
    setLocationsFilterLabel(label);
  }, [localFilters.locations]);

  return [locationsFilterLabel];
};

export default useLocationFilter;
