import { defineStore } from 'pinia';
import { ref } from 'vue';
import api, { AuthApi } from '../services/api';

export const useAuthStore = defineStore('auth', () => {
  const isPasscodeRequired = ref(false);
  const isAuthenticated = ref(false);
  const token = ref<string | null>(localStorage.getItem('omnom_token'));

  const checkStatus = async () => {
    try {
      const res = await AuthApi.getStatus();
      isPasscodeRequired.value = res.passcodeRequired;
      isAuthenticated.value = res.authenticated;
      if (!res.authenticated) {
        token.value = null;
        localStorage.removeItem('omnom_token');
      }
    } catch {
      isPasscodeRequired.value = true;
      isAuthenticated.value = false;
    }
  };

  const loginWithPasscode = async (passcode: string) => {
    const res = await AuthApi.verify(passcode);
    if (res.success) {
      token.value = res.token;
      localStorage.setItem('omnom_token', res.token);
      isAuthenticated.value = true;
      return true;
    }
    return false;
  };

  const logout = () => {
    token.value = null;
    localStorage.removeItem('omnom_token');
    isAuthenticated.value = !isPasscodeRequired.value;
  };

  api.interceptors.response.use(response => response, err => {
    if (err.response?.status === 401 && err.config?.url !== '/auth/verify') {
      isPasscodeRequired.value = true;
      logout();
    }
    return Promise.reject(err);
  });

  return {
    isPasscodeRequired,
    isAuthenticated,
    token,
    checkStatus,
    loginWithPasscode,
    logout,
  };
});
