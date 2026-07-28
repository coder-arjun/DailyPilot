import { Ionicons } from '@expo/vector-icons';
import * as Location from 'expo-location';
import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { Linking, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Button, Card, ErrorText, Input, Label, Muted, Screen } from '@/components/ui';
import { apiErrorMessage } from '@/lib/api/client';
import { theme } from '@/lib/theme';
import { formatKm, haversineKm, useResolvePlace, type ResolvedPlace } from '@/features/routeplanner/api';

const { colors, radius, spacing } = theme;

const MIN_STOPS = 2;
const MAX_STOPS = 5;

type Stop = {
  id: number;
  text: string;
  point: ResolvedPlace | null;
  error: string | null;
  resolving: boolean;
};

let nextStopId = 0;
const newStop = (): Stop => ({ id: nextStopId++, text: '', point: null, error: null, resolving: false });

export default function RoutePlannerScreen() {
  const router = useRouter();
  const resolvePlace = useResolvePlace();

  const [you, setYou] = useState<ResolvedPlace | null>(null);
  const [locating, setLocating] = useState(false);
  const [locateError, setLocateError] = useState('');

  // Manual fallback for the start point — always available (web parity with
  // Views/RoutePlanner/Index.cshtml's "rpYouManual"), resolved through the same
  // server endpoint as stops, and emphasized when geolocation fails.
  const [manualText, setManualText] = useState('');
  const [manualResolving, setManualResolving] = useState(false);
  const [manualError, setManualError] = useState('');

  const [stops, setStops] = useState<Stop[]>(() => [newStop(), newStop()]);

  async function onUseLocation() {
    setLocateError('');
    setLocating(true);
    try {
      const perm = await Location.requestForegroundPermissionsAsync();
      if (perm.status !== 'granted') {
        setLocateError('Location permission was denied — allow it in your device settings, or enter your start manually below.');
        return;
      }
      const pos = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });
      setYou({ lat: pos.coords.latitude, lng: pos.coords.longitude, label: 'Your location' });
      setManualText('');
      setManualError('');
    } catch {
      setLocateError('Couldn’t get a fix on your location — try again, or enter your start manually below.');
    } finally {
      setLocating(false);
    }
  }

  async function resolveYouManually() {
    const text = manualText.trim();
    if (!text || manualResolving) return;
    setManualError('');
    setManualResolving(true);
    try {
      const point = await resolvePlace.mutateAsync(text);
      setYou({ ...point, label: point.label || 'Your start' });
      setLocateError('');
      setManualResolving(false);
    } catch (e) {
      setManualResolving(false);
      setManualError(apiErrorMessage(e));
    }
  }

  function clearYou() {
    setYou(null);
    setLocateError('');
    setManualError('');
    setManualText('');
  }

  function updateStop(id: number, patch: Partial<Stop>) {
    setStops((prev) => prev.map((s) => (s.id === id ? { ...s, ...patch } : s)));
  }

  async function resolveStop(id: number) {
    const stop = stops.find((s) => s.id === id);
    if (!stop || !stop.text.trim() || stop.resolving) return;
    updateStop(id, { resolving: true, error: null });
    try {
      const point = await resolvePlace.mutateAsync(stop.text.trim());
      updateStop(id, { point, resolving: false, error: null });
    } catch (e) {
      updateStop(id, { point: null, resolving: false, error: apiErrorMessage(e) });
    }
  }

  function addStop() {
    setStops((prev) => (prev.length >= MAX_STOPS ? prev : [...prev, newStop()]));
  }

  function removeStop(id: number) {
    setStops((prev) => (prev.length <= MIN_STOPS ? prev : prev.filter((s) => s.id !== id)));
  }

  function clearStop(id: number) {
    updateStop(id, { point: null, error: null, text: '' });
  }

  const ranked = useMemo(() => {
    if (!you) return [];
    return stops
      .filter((s): s is Stop & { point: ResolvedPlace } => !!s.point)
      .map((s) => ({ stop: s, km: haversineKm(you, s.point) }))
      .sort((a, b) => a.km - b.km);
  }, [you, stops]);

  const ready = !!you && ranked.length >= MIN_STOPS;

  function onOpenRoute() {
    if (!you || ranked.length < MIN_STOPS) return;
    const origin = `${you.lat},${you.lng}`;
    const pts = ranked.map((r) => `${r.stop.point.lat},${r.stop.point.lng}`);
    const destination = pts[pts.length - 1];
    const waypoints = pts.slice(0, -1);
    let url = `https://www.google.com/maps/dir/?api=1&origin=${encodeURIComponent(origin)}&destination=${encodeURIComponent(destination)}&travelmode=driving`;
    if (waypoints.length) url += `&waypoints=${encodeURIComponent(waypoints.join('|'))}`;
    Linking.openURL(url);
  }

  return (
    <Screen>
      <View style={styles.header}>
        <Pressable hitSlop={10} onPress={() => router.back()} accessibilityLabel="Back">
          <Ionicons name="arrow-back" size={22} color={colors.text} />
        </Pressable>
        <Text style={styles.title}>Route Planner</Text>
      </View>
      <ScrollView contentContainerStyle={styles.body} keyboardShouldPersistTaps="handled">
        <Muted>Compare stops by straight-line distance and open the ranked route in Google Maps.</Muted>

        <Card style={styles.card}>
          <Label>Your location</Label>
          {you ? (
            <Pressable style={styles.chip} onPress={clearYou}>
              <Ionicons name="checkmark-circle" size={16} color={colors.success} />
              <Text style={styles.chipText}>{you.label}</Text>
              <Text style={styles.chipCoords}>
                {you.lat.toFixed(5)}, {you.lng.toFixed(5)}
              </Text>
            </Pressable>
          ) : (
            <>
              <Button title="Use my location" variant="ghost" onPress={onUseLocation} loading={locating} />
              <ErrorText>{locateError}</ErrorText>

              <Text style={[styles.manualHeading, locateError ? styles.manualHeadingEmphasis : null]}>
                {locateError ? 'Enter your start manually instead' : 'Or enter your start manually'}
              </Text>
              <Input
                value={manualText}
                onChangeText={setManualText}
                onBlur={resolveYouManually}
                onSubmitEditing={resolveYouManually}
                placeholder="Paste a Google Maps link or lat,lng"
                autoCapitalize="none"
                style={locateError ? styles.manualInputEmphasis : undefined}
              />
              {manualResolving ? <Muted>Resolving…</Muted> : null}
              <ErrorText>{manualError}</ErrorText>
            </>
          )}
        </Card>

        {stops.map((stop, i) => (
          <Card key={stop.id} style={styles.card}>
            <View style={styles.stopHeader}>
              <Label>Stop {i + 1}</Label>
              {stops.length > MIN_STOPS ? (
                <Pressable hitSlop={8} onPress={() => removeStop(stop.id)} accessibilityLabel={`Remove stop ${i + 1}`}>
                  <Ionicons name="close-circle-outline" size={18} color={colors.muted} />
                </Pressable>
              ) : null}
            </View>
            {stop.point ? (
              <Pressable style={styles.chip} onPress={() => clearStop(stop.id)}>
                <Ionicons name="checkmark-circle" size={16} color={colors.success} />
                <Text style={styles.chipText}>{stop.point.label ?? `Stop ${i + 1}`}</Text>
                <Text style={styles.chipCoords}>
                  {stop.point.lat.toFixed(5)}, {stop.point.lng.toFixed(5)}
                </Text>
              </Pressable>
            ) : (
              <>
                <Input
                  value={stop.text}
                  onChangeText={(text) => updateStop(stop.id, { text })}
                  onBlur={() => resolveStop(stop.id)}
                  onSubmitEditing={() => resolveStop(stop.id)}
                  placeholder="Paste a Google Maps link or lat,lng"
                  autoCapitalize="none"
                />
                {stop.resolving ? <Muted>Resolving…</Muted> : null}
                <ErrorText>{stop.error}</ErrorText>
              </>
            )}
          </Card>
        ))}

        {stops.length < MAX_STOPS ? <Button title="Add stop" variant="ghost" onPress={addStop} /> : null}

        <Card style={[styles.card, { marginTop: spacing(4) }]}>
          <Text style={styles.sectionTitle}>Ranked by distance</Text>
          {!ready ? (
            <Muted>Share your location and resolve at least two stops to compare.</Muted>
          ) : (
            <>
              {ranked.map((r, i) => (
                <View key={r.stop.id} style={styles.rankRow}>
                  <View style={styles.rankPos}>
                    <Text style={styles.rankPosText}>{i + 1}</Text>
                  </View>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.rankName}>{r.stop.point.label ?? `Stop ${stops.indexOf(r.stop) + 1}`}</Text>
                    <Muted>{formatKm(r.km)}</Muted>
                  </View>
                  {i === 0 ? (
                    <View style={styles.nearestBadge}>
                      <Text style={styles.nearestText}>Nearest</Text>
                    </View>
                  ) : null}
                </View>
              ))}
              <View style={{ height: spacing(3) }} />
              <Button title="Open route in Google Maps" onPress={onOpenRoute} />
            </>
          )}
        </Card>
        <View style={{ height: spacing(10) }} />
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    paddingHorizontal: spacing(5),
    paddingTop: spacing(3),
    paddingBottom: spacing(1),
  },
  body: { padding: spacing(5), paddingTop: spacing(1.5) },
  title: { color: colors.text, fontSize: 24, fontWeight: '800' },
  card: { marginTop: spacing(4) },
  stopHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(2),
    height: 44,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.success,
    backgroundColor: colors.success + '18',
    paddingHorizontal: spacing(3.5),
  },
  chipText: { color: colors.text, fontWeight: '600', fontSize: 14, flexShrink: 1 },
  chipCoords: { color: colors.muted, fontSize: 12, marginLeft: 'auto' },
  manualHeading: { color: colors.muted, fontSize: 13, fontWeight: '600', marginTop: spacing(3), marginBottom: spacing(1.5) },
  manualHeadingEmphasis: { color: colors.accent, fontWeight: '700' },
  manualInputEmphasis: { borderColor: colors.accent },
  sectionTitle: { color: colors.text, fontSize: 16, fontWeight: '700', marginBottom: spacing(2) },
  rankRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing(3),
    paddingVertical: spacing(2.5),
    borderBottomWidth: 1,
    borderBottomColor: colors.border,
  },
  rankPos: {
    width: 26,
    height: 26,
    borderRadius: 13,
    backgroundColor: colors.surface2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  rankPosText: { color: colors.text, fontWeight: '700', fontSize: 13 },
  rankName: { color: colors.text, fontSize: 15, fontWeight: '600' },
  nearestBadge: {
    backgroundColor: colors.accent + '22',
    borderColor: colors.accent,
    borderWidth: 1,
    borderRadius: 999,
    paddingHorizontal: spacing(2.5),
    paddingVertical: spacing(1),
  },
  nearestText: { color: colors.accent, fontWeight: '700', fontSize: 11, textTransform: 'uppercase' },
});
