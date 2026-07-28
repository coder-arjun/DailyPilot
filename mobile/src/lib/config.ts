import Constants from 'expo-constants';

const fallback = 'https://dailypilot.runasp.net/api/v1';

export const config = {
  apiUrl: (Constants.expoConfig?.extra?.apiUrl as string | undefined) ?? fallback,
};
