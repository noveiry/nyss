import { useEffect, useReducer } from "react";
import {
  Card,
  CardContent,
  TextField,
  MenuItem,
  Grid,
  Radio,
  RadioGroup,
  FormControlLabel,
  InputLabel,
  useTheme,
  useMediaQuery,
  makeStyles,
} from "@material-ui/core";
import { strings, stringKeys } from "../../../strings";
import useDebounce from "../../../utils/debounce";
import * as roles from "../../../authentication/roles";
import { trainingStatus } from "../logic/dataCollectorsConstants";
import LocationFilter from "../../common/filters/LocationFilter";
import useLocalFilters from "../../common/filters/useLocalFilters";
import useLocationFilter from "../../common/filters/useLocationFilter";
import { DrawerFilter } from "../../common/filters/DrawerFilter";

const useStyles = makeStyles((theme) => ({
  selectFilterItem: {
    width: "150px",
    [theme.breakpoints.down("xs")]: {
      width: "100%",
      maxWidth: "220px",
    },
  },
  filterRadioGroup: {
    paddingTop: "5px",
  },
  radio: {
    height: "23px",
  },
}));

export const DataCollectorsPerformanceFilters = ({
  onChange,
  filters,
  rtl,
  locations,
  supervisors,
  userRoles,
}) => {
  //Reducer for local filters state
  const [localFilters, updateLocalFilters] = useLocalFilters(filters);

  //Fetches new data based on changes in filters
  const handleFiltersChange = (filters) =>
    onChange(updateLocalFilters(filters));

  //Syncs locations from redux store with filter state and sets label for location filter to 'All' or "Region (+n)"
  //Neccecary if locations are added, edited or removed, to make all filters checked
  const [locationsFilterLabel] = useLocationFilter(
    locations,
    localFilters,
    updateLocalFilters,
  );

  const theme = useTheme();
  const classes = useStyles();
  const isSmallScreen = useMediaQuery(theme.breakpoints.down("xs"));
  const isMediumScreen = useMediaQuery(theme.breakpoints.down("sm"));

  const handleAreaChange = (newValue) =>
    handleFiltersChange({ locations: newValue, pageNumber: 1 });

  const handleSupervisorChange = (event) =>
    handleFiltersChange({
      supervisorId: event.target.value === 0 ? null : event.target.value,
    });

  const handleTrainingStatusChange = (event) =>
    handleFiltersChange({ trainingStatus: event.target.value });

  if (!filters) {
    return null;
  }

  const Filter = () => {
    const [name, setName] = useReducer(
      (state, action) => {
        if (state.value !== action) {
          return { changed: true, value: action };
        } else {
          return state;
        }
      },
      { value: localFilters.name, changed: false },
    );

    const debouncedName = useDebounce(name, 500);

    const handleNameChange = (event) => setName(event.target.value);

    useEffect(() => {
      debouncedName.changed &&
        handleFiltersChange({ name: debouncedName.value });
    }, [debouncedName.value]);

    return (
      <Grid container spacing={2} direction={"row"} alignItems={"flex-start"}>
        <Grid item xs={isSmallScreen ? 6 : null}>
          <TextField
            label={strings(stringKeys.common.name)}
            onChange={handleNameChange}
            value={name.value}
            className={classes.selectFilterItem}
            InputLabelProps={{ shrink: true }}
          />
        </Grid>
        <Grid item xs={isSmallScreen ? 6 : null}>
          <LocationFilter
            allLocations={locations}
            filteredLocations={filters.locations}
            filterLabel={locationsFilterLabel}
            onChange={handleAreaChange}
            rtl={rtl}
          />
        </Grid>

        {!userRoles.some((r) => r === roles.Supervisor) && (
          <Grid item xs={isSmallScreen ? 6 : null}>
            <TextField
              select
              label={strings(stringKeys.dataCollectors.filters.supervisors)}
              onChange={handleSupervisorChange}
              value={filters.supervisorId || 0}
              className={classes.selectFilterItem}
              InputLabelProps={{ shrink: true }}
              SelectProps={{
                MenuProps: {
                  PaperProps: {
                    style: {
                      maxWidth: "90%",
                    },
                  },
                },
              }}
            >
              <MenuItem value={0}>
                {strings(stringKeys.dataCollectors.filters.supervisorsAll)}
              </MenuItem>

              {supervisors.map((supervisor) => (
                <MenuItem
                  key={`filter_supervisor_${supervisor.id}`}
                  value={supervisor.id}
                >
                  {supervisor.name}
                </MenuItem>
              ))}
            </TextField>
          </Grid>
        )}

        <Grid item xs={isSmallScreen ? 6 : null}>
          <InputLabel>
            {strings(stringKeys.dataCollectors.filters.trainingStatus)}
          </InputLabel>
          <RadioGroup
            value={localFilters.trainingStatus || "All"}
            onChange={handleTrainingStatusChange}
            className={classes.filterRadioGroup}
          >
            {trainingStatus.map((status) => (
              <FormControlLabel
                key={`trainingStatus_filter_${status}`}
                control={<Radio />}
                className={classes.radio}
                label={strings(
                  stringKeys.dataCollectors.constants.trainingStatus[status],
                )}
                value={status}
              />
            ))}
          </RadioGroup>
        </Grid>
      </Grid>
    );
  };

  if (isMediumScreen) {
    return (
      <Grid container justifyContent="center" style={{ marginBottom: 20 }}>
        <DrawerFilter
          title={strings(stringKeys.dataCollectors.title)}
          children={<Filter />}
          showResults={handleFiltersChange}
        />
      </Grid>
    );
  }

  return (
    <Card>
      <CardContent>
        <Filter />
      </CardContent>
    </Card>
  );
};
