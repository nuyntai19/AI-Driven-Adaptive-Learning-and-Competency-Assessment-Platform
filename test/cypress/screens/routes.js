// UI navigation only. API routes stay explicit in API specs as contract assertions.
const routes = Object.freeze({
  login: '/dang-nhap',
  accessDenied: '/khong-co-quyen',
  platformCenters: '/quan-tri-nen-tang/trung-tam',
  teacherReview: '/giao-vien/cham-bai',
  managerClasses: '/quan-ly/lop-hoc',
  studentAssignments: '/hoc-tap/bai-tap',
  studentOverview: '/hoc-tap/tong-quan',
  teacherQuestion: id => `/giao-vien/cau-hoi/${encodeURIComponent(id)}`,
});
module.exports = { routes };
