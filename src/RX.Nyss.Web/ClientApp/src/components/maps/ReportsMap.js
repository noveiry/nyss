import React, { useEffect, useState, useRef } from 'react';
import { MapContainer, TileLayer, ScaleControl, useMap } from 'react-leaflet';
import { calculateBounds, calculateCenter, calculateIconSize } from '../../utils/map';
import { TextIcon } from "../common/map/MarkerIcon";
import { MarkerClusterComponent } from "./MarkerClusterComponent";

export const ReportsMap = ({ data, details, detailsFetching, onMarkerClick }) => {
  const [bounds, setBounds] = useState(null);
  const [center, setCenter] = useState(null);
  const [zoom, setZoom] = useState(2);
  const [totalReports, setTotalReports] = useState(null);
  const [hasInteractedWithMap, setHasInteractedWithMap] = useState(false);

  useEffect(() => {
    if (!data) {
      return;
    }
    setHasInteractedWithMap(false);
    const totalReportCount = data.reduce((a, d) => a + d.reportsCount, 0);
    setTotalReports(totalReportCount);

    // If there are no reports zoom is decreased creating a more generic map.
    setZoom(totalReportCount >= 1 ? 5 : 2);
    setBounds(data.length > 1 ? calculateBounds(data) : null);  
    setCenter(data.length > 1 ? null : calculateCenter(data.map(l => ({ lat: l.location.latitude, lng: l.location.longitude }))));
  }, [data]);

  // Re-center / update map bounds when the center, bounds or map change.
  const MapViewController = (center) => {
    const map = useMap();
    
    // Avoid updates when user is interacting with the map. 
    useEffect(() => {
        if(!hasInteractedWithMap) {
          if (center.center) {
            // If there are no reports, use [0,0] as a default value for a more generic world map.
            const centerLatLong = totalReports > 0 ? center.center : [20,0];
            map.setView(centerLatLong, zoom);
          }
          if (bounds) {
            map.fitBounds(bounds);
          }
      }
      }, [center, map, bounds]);

    // Returns null such that it can be rendered as a react component.
    return null;
  }

  const createClusterIcon = (cluster) => {
    const count = cluster.getAllChildMarkers().reduce((sum, item) => item.options.reportsCount + sum, 0);
    return new TextIcon({
      size: calculateIconSize(count, totalReports),
      text: count
    });
  }

  const createIcon = (count) => {
    return new TextIcon({
      size: calculateIconSize(count, totalReports),
      text: count
    });
  }

  const handleMarkerClick = e => {
    setHasInteractedWithMap(true);
    onMarkerClick(e.latlng.lat, e.latlng.lng);
  }

  let resizeObserver = useRef(null);

  const setMap = map => {
    resizeObserver.current = new ResizeObserver(() => {
        map.invalidateSize();
    });
    const container = document.getElementById("map-container");
    resizeObserver.current.observe(container);
  };

  return (!!center || !!bounds) && (
    <MapContainer
      id='map-container'
      style={{ minHeight: "100%", borderRadius: 8 }}
      zoom={5}
      bounds={bounds}
      center={center}
      scrollWheelZoom={false}
      maxZoom={19}
      whenCreated={setMap}
    >
      <TileLayer
        attribution='&copy; <a href="http://osm.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      <MarkerClusterComponent
        data={data}
        details={details}
        detailsFetching={detailsFetching}
        createIcon={createIcon}
        createClusterIcon={createClusterIcon}
        handleMarkerClick={handleMarkerClick}
      />

      <ScaleControl imperial={false}></ScaleControl>
      {/* CenterMapController must be added as a component since the useMap hook must be initialized inside a MapContainer.*/}
      <MapViewController center={center} />
    </MapContainer>
  );
}
