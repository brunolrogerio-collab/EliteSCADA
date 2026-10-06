import React from 'react';
import { AlarmEditor as SecuredAlarmEditor } from './SecuredEngineeringEditors';
import type { EngineeringLocale } from './i18n';
import type { EngineeringPackageView } from './types';

export function AlarmEditor({ model, locale, projectKey }: { model: EngineeringPackageView; locale: EngineeringLocale; projectKey?: string }) {
  return <SecuredAlarmEditor model={model} locale={locale} projectKey={projectKey} />;
}
